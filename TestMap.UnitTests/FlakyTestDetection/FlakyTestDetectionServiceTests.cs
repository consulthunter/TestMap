using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;
using TestMap.Services.FlakyTestDetection;

namespace TestMap.UnitTests.FlakyTestDetection;

/// <summary>
/// Unit tests for <see cref="FlakyTestDetectionService"/> using hand-written test doubles
/// for all three dependencies (<see cref="IFlakinessScoringService"/>,
/// <see cref="ITestExecutionHistoryService"/>, <see cref="IFlakyTestRerunService"/>).
/// Covers the disabled/invalid-config short-circuits, per-result scoring, and rerun routing.
/// </summary>
public sealed class FlakyTestDetectionServiceTests
{
    /// <summary>
    /// When the config has Enabled=false, DetectAsync returns empty Scores and RerunResults
    /// collections without calling any dependency.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DetectAsync_Disabled_ReturnsEmptyCollections()
    {
        var rerunSpy = new SpyRerunService([]);
        var service = BuildService(isValid: true, rerunSpy: rerunSpy);

        var result = await service.DetectAsync(new FlakyTestDetectionRequest(
            "run-1",
            [MakeResult("Test1")],
            new FlakyTestDetectionConfig { Enabled = false }));

        Assert.Empty(result.Scores);
        Assert.Empty(result.RerunResults);
        Assert.Equal(0, rerunSpy.CallCount); // rerun service never invoked
    }

    /// <summary>
    /// When the scoring service reports that the configuration is invalid, DetectAsync
    /// throws InvalidOperationException whose message contains the validation error text.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DetectAsync_InvalidConfig_ThrowsInvalidOperationException()
    {
        var service = BuildService(isValid: false, validationError: "Weight must be between 0.01 and 0.99.");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DetectAsync(new FlakyTestDetectionRequest(
                "run-1",
                [MakeResult("Test1")],
                ValidConfig())));

        Assert.Contains("Invalid flaky test detection configuration", ex.Message);
        Assert.Contains("Weight must be between 0.01 and 0.99.", ex.Message);
    }

    /// <summary>
    /// DetectAsync calls ScoreAsync once for every entry in CurrentResults, so the returned
    /// Scores collection has exactly as many entries as the input.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DetectAsync_EachCurrentResultProducesExactlyOneScore()
    {
        var service = BuildService(isValid: true);

        var result = await service.DetectAsync(new FlakyTestDetectionRequest(
            "run-1",
            [MakeResult("TestA"), MakeResult("TestB"), MakeResult("TestC")],
            ValidConfig()));

        Assert.Equal(3, result.Scores.Count);
    }

    /// <summary>
    /// When RerunFailedTests is false, the rerun service is never called even when there
    /// are failed tests in the current results.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DetectAsync_RerunDisabled_DoesNotCallRerunService()
    {
        var rerunSpy = new SpyRerunService([]);
        var service = BuildService(isValid: true, rerunSpy: rerunSpy);

        var config = ValidConfig();
        config.RerunFailedTests = false;

        await service.DetectAsync(new FlakyTestDetectionRequest(
            "run-1",
            [MakeResult("FailingTest", outcome: "Failed")],
            config));

        Assert.Equal(0, rerunSpy.CallCount);
    }

    /// <summary>
    /// When RerunFailedTests is true, the rerun service is called exactly once and receives
    /// only the failing tests (not the passing ones) from CurrentResults.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DetectAsync_RerunEnabled_CallsRerunServiceWithOnlyFailedTests()
    {
        var rerunSpy = new SpyRerunService([]);
        var service = BuildService(isValid: true, rerunSpy: rerunSpy);

        var config = ValidConfig();
        config.RerunFailedTests = true;
        config.RerunCount = 2;

        await service.DetectAsync(new FlakyTestDetectionRequest(
            "run-1",
            [
                MakeResult("PassingTest", outcome: "Passed"),
                MakeResult("FailingTest", outcome: "Failed"),
                MakeResult("AnotherFail",  outcome: "Error")
            ],
            config));

        Assert.Equal(1, rerunSpy.CallCount);
        Assert.Equal(2, rerunSpy.LastFailedTestCount); // only the 2 non-passing results
    }

    // ─── Infrastructure ──────────────────────────────────────────────────────

    private static FlakyTestDetectionService BuildService(
        bool isValid,
        SpyRerunService? rerunSpy = null,
        string? validationError = null)
    {
        return new FlakyTestDetectionService(
            new SpyScoringService(isValid, validationError),
            new SpyHistoryService(),
            rerunSpy ?? new SpyRerunService([]));
    }

    private static FlakyTestDetectionConfig ValidConfig() => new()
    {
        Enabled = true,
        MinimumExecutions = 3,
        HistoryWindowRuns = 20,
        RerunFailedTests = false,
        RerunCount = 2,
        FlakyScoreThreshold = 60.0
    };

    private static TestExecutionResultModel MakeResult(
        string testName,
        string outcome = "Passed") =>
        new() { TestName = testName, Outcome = outcome, RunId = "run-1" };

    // ─── Test doubles ────────────────────────────────────────────────────────

    /// <summary>
    /// Scoring service stub. Returns a fixed validation result and a minimal
    /// <see cref="FlakyTestScoreModel"/> (testName copied from the last history entry).
    /// </summary>
    private sealed class SpyScoringService(bool isValid, string? validationError = null)
        : IFlakinessScoringService
    {
        public FlakinessScoringValidationResult Validate(FlakyTestDetectionConfig config) =>
            isValid
                ? FlakinessScoringValidationResult.Success()
                : new FlakinessScoringValidationResult(false,
                    validationError is not null ? [validationError] : ["Validation failed."]);

        public Task<FlakyTestScoreModel> ScoreAsync(
            string runId,
            IReadOnlyList<TestExecutionResultModel> testHistory,
            FlakyTestDetectionConfig config,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FlakyTestScoreModel
            {
                RunId = runId,
                TestName = testHistory.LastOrDefault()?.TestName ?? string.Empty,
                Classification = FlakyTestClassification.InsufficientData
            });
    }

    /// <summary>
    /// History service stub. Returns an empty history list for every test identity,
    /// forcing the scoring service to handle the insufficient-data case.
    /// </summary>
    private sealed class SpyHistoryService : ITestExecutionHistoryService
    {
        public Task<IReadOnlyList<TestExecutionResultModel>> GetHistoryAsync(
            TestExecutionResultModel testIdentity,
            int historyWindowRuns,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TestExecutionResultModel>>([]);
    }

    /// <summary>
    /// Rerun service spy. Records how many times it was called and how many failed tests
    /// were passed on the most recent call, then returns the provided <paramref name="reruns"/>.
    /// </summary>
    private sealed class SpyRerunService(IReadOnlyList<FlakyTestRerunResultModel> reruns)
        : IFlakyTestRerunService
    {
        public int CallCount { get; private set; }
        public int LastFailedTestCount { get; private set; }

        public Task<IReadOnlyList<FlakyTestRerunResultModel>> RerunFailedTestsAsync(
            string runId,
            IReadOnlyList<TestExecutionResultModel> failedTests,
            FlakyTestDetectionConfig config,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastFailedTestCount = failedTests.Count;
            return Task.FromResult(reruns);
        }
    }
}
