using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;
using TestMap.Services.FlakyTestDetection;

namespace TestMap.UnitTests.FlakyTestDetection;

/// <summary>
/// Unit tests for <see cref="FlakinessScoringService"/> covering configuration validation,
/// weighted score calculation, evidence aggregation, and threshold-based classification.
/// All tests use a <see cref="FixedScoreProvider"/> test double that returns a preset
/// score for each factor kind so the arithmetic can be verified deterministically.
/// </summary>
public sealed class FlakinessScoringServiceTests
{
    // ── Validate ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A fully valid config — correct weights summing to 1.0, all five factor providers
    /// registered, MinimumExecutions and HistoryWindowRuns in range — passes validation.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_ValidConfig_ReturnsSuccess()
    {
        var service = BuildService(providerScore: 0.5);

        var result = service.Validate(ValidConfig());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// A weight of 0.005 (below the minimum 0.01) for OutcomeVariance produces a validation
    /// error that names the offending factor.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WeightBelowMinimum_ReturnsErrorNamingFactor()
    {
        var service = BuildService(providerScore: 0.5);
        var config = ValidConfig();
        // OutcomeVariance 0.005 is below 0.01; other weights adjusted so sum stays at 1.0
        config.Weights = new FlakinessWeightsConfig
        {
            OutcomeVariance = 0.005,
            RerunInstability = 0.25,
            DurationVariance = 0.25,
            FailureSignature = 0.25,
            EnvironmentSignal = 0.245
        };

        var result = service.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("OutcomeVariance"));
    }

    /// <summary>
    /// When the five weights sum to 1.10 instead of 1.0, validation returns an error
    /// that mentions "sum".
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_WeightsDoNotSumToOne_ReturnsErrorMentioningSum()
    {
        var service = BuildService(providerScore: 0.5);
        var config = ValidConfig();
        // Increase OutcomeVariance from 0.35 → 0.45; total becomes 1.10
        config.Weights = new FlakinessWeightsConfig
        {
            OutcomeVariance = 0.45,
            RerunInstability = 0.25,
            DurationVariance = 0.15,
            FailureSignature = 0.15,
            EnvironmentSignal = 0.10
        };

        var result = service.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("sum"));
    }

    /// <summary>
    /// Setting MinimumExecutions to 1 (below the required minimum of 2) produces a
    /// validation error mentioning "MinimumExecutions".
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_MinimumExecutionsBelowTwo_ReturnsError()
    {
        var service = BuildService(providerScore: 0.5);
        var config = ValidConfig();
        config.MinimumExecutions = 1;

        var result = service.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MinimumExecutions"));
    }

    /// <summary>
    /// HistoryWindowRuns=3 with MinimumExecutions=5 violates the constraint that
    /// the window must be at least as large as the minimum, producing a validation error.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_HistoryWindowLessThanMinimumExecutions_ReturnsError()
    {
        var service = BuildService(providerScore: 0.5);
        var config = ValidConfig();
        config.MinimumExecutions = 5;
        config.HistoryWindowRuns = 3; // 3 < 5

        var result = service.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("HistoryWindowRuns"));
    }

    /// <summary>
    /// Enabling RerunFailedTests while setting RerunCount=0 is invalid — validation
    /// must return an error that mentions "RerunCount".
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_RerunEnabledWithZeroRerunCount_ReturnsError()
    {
        var service = BuildService(providerScore: 0.5);
        var config = ValidConfig();
        config.RerunFailedTests = true;
        config.RerunCount = 0;

        var result = service.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("RerunCount"));
    }

    /// <summary>
    /// If no provider is registered for a factor whose weight appears in the config, validation
    /// returns an error naming the unregistered factor kind.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_MissingFactorProvider_ReturnsErrorNamingMissingFactor()
    {
        // Register only 4 of 5 providers — OutcomeVariance is absent
        var service = new FlakinessScoringService([
            new FixedScoreProvider(FlakinessFactorKind.RerunInstability, 0.5),
            new FixedScoreProvider(FlakinessFactorKind.DurationVariance, 0.5),
            new FixedScoreProvider(FlakinessFactorKind.FailureSignature, 0.5),
            new FixedScoreProvider(FlakinessFactorKind.EnvironmentSignal, 0.5),
        ]);

        var result = service.Validate(ValidConfig());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("OutcomeVariance"));
    }

    // ── ScoreAsync ────────────────────────────────────────────────────────────

    /// <summary>
    /// Passing an empty history list returns InsufficientData immediately; the
    /// evidence explains that no history was provided.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_EmptyHistory_ReturnsInsufficientData()
    {
        var service = BuildService(providerScore: 1.0);

        var result = await service.ScoreAsync("run-1", [], ValidConfig());

        Assert.Equal(FlakyTestClassification.InsufficientData, result.Classification);
        Assert.Single(result.Evidence);
        Assert.Contains("No test execution history", result.Evidence[0]);
    }

    /// <summary>
    /// When the history has fewer entries than MinimumExecutions, ScoreAsync returns
    /// InsufficientData and the evidence message includes the actual and required counts.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_HistoryBelowMinimumExecutions_ReturnsInsufficientData()
    {
        var service = BuildService(providerScore: 1.0);
        var config = ValidConfig();
        config.MinimumExecutions = 3;

        var history = new List<TestExecutionResultModel>
        {
            MakeResult("FlakeyTest"), MakeResult("FlakeyTest") // 2 < 3 required
        };

        var result = await service.ScoreAsync("run-1", history, config);

        Assert.Equal(FlakyTestClassification.InsufficientData, result.Classification);
        var evidence = Assert.Single(result.Evidence);
        Assert.Contains("2", evidence); // actual count
        Assert.Contains("3", evidence); // required count
    }

    /// <summary>
    /// When all five providers return a score of 1.0, the weighted sum equals 1.0 × 100 = 100.
    /// A score of 100 with a threshold of 60 is classified as LikelyFlaky.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_AllProvidersReturnMaxScore_FinalScoreIs100AndLikelyFlaky()
    {
        var service = BuildService(providerScore: 1.0);
        var config = ValidConfig();
        config.FlakyScoreThreshold = 60.0;
        var history = MakeHistory("FlakeyTest", count: 5);

        var result = await service.ScoreAsync("run-1", history, config);

        Assert.Equal(100.0, result.FlakinessScore, precision: 3);
        Assert.Equal(FlakyTestClassification.LikelyFlaky, result.Classification);
    }

    /// <summary>
    /// When all providers return 0.35, the weighted sum equals 0.35 × 1.0 = 0.35 → 35.
    /// A score of 35 is at or above threshold/2 (30) but below threshold (60), so the
    /// classification is PotentiallyFlaky.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_MidRangeProviderScore_ClassifiesAsPotentiallyFlaky()
    {
        // weights default to 0.35+0.25+0.15+0.15+0.10 = 1.00
        // each provider returns 0.35 → weightedSum = 0.35×1.0 = 0.35 → finalScore = 35.0
        var service = BuildService(providerScore: 0.35);
        var config = ValidConfig();
        config.FlakyScoreThreshold = 60.0;
        var history = MakeHistory("SuspectTest", count: 5);

        var result = await service.ScoreAsync("run-1", history, config);

        Assert.Equal(35.0, result.FlakinessScore, precision: 3);
        Assert.Equal(FlakyTestClassification.PotentiallyFlaky, result.Classification);
    }

    /// <summary>
    /// When all providers return 0.2, the final score is 20.0 — below threshold/2 (30) —
    /// and the test is classified as Stable.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_LowProviderScore_ClassifiesAsStable()
    {
        var service = BuildService(providerScore: 0.2); // 0.2×1.0 × 100 = 20.0
        var config = ValidConfig();
        config.FlakyScoreThreshold = 60.0;
        var history = MakeHistory("StableTest", count: 5);

        var result = await service.ScoreAsync("run-1", history, config);

        Assert.Equal(20.0, result.FlakinessScore, precision: 3);
        Assert.Equal(FlakyTestClassification.Stable, result.Classification);
    }

    /// <summary>
    /// Provider evidence strings that are non-empty are collected into the result's Evidence
    /// list; empty evidence strings are omitted.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_NonEmptyProviderEvidenceCollectedAndEmptyOmitted()
    {
        var service = new FlakinessScoringService([
            new FixedScoreProvider(FlakinessFactorKind.OutcomeVariance,  0.5, "outcome evidence"),
            new FixedScoreProvider(FlakinessFactorKind.RerunInstability, 0.5, ""),           // omitted
            new FixedScoreProvider(FlakinessFactorKind.DurationVariance, 0.5, "duration evidence"),
            new FixedScoreProvider(FlakinessFactorKind.FailureSignature, 0.5, ""),           // omitted
            new FixedScoreProvider(FlakinessFactorKind.EnvironmentSignal, 0.5, "   "),       // whitespace — omitted
        ]);
        var history = MakeHistory("T", count: 5);

        var result = await service.ScoreAsync("run-1", history, ValidConfig());

        Assert.Contains("outcome evidence", result.Evidence);
        Assert.Contains("duration evidence", result.Evidence);
        Assert.Equal(2, result.Evidence.Count);
    }

    /// <summary>
    /// The FactorScores dictionary in the result contains an entry for each of the five
    /// factor kinds, each equal to the normalised provider score.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_FactorScoresDictionaryPopulatedForAllFiveFactors()
    {
        var service = BuildService(providerScore: 0.7);
        var history = MakeHistory("T", count: 5);

        var result = await service.ScoreAsync("run-1", history, ValidConfig());

        Assert.Equal(5, result.FactorScores.Count);
        Assert.All(result.FactorScores.Values, score => Assert.Equal(0.7, score, precision: 3));
    }

    /// <summary>
    /// The TestName, TestMemberId, and FilePath in the result are taken from the
    /// last (most recent) item in the history list.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_MetadataCopiedFromLatestHistoryEntry()
    {
        var service = BuildService(providerScore: 0.5);
        var history = new List<TestExecutionResultModel>
        {
            MakeResult("EarlyRun",  testMemberId: 99, filePath: "Tests/Old.cs"),
            MakeResult("LatestRun", testMemberId: 42, filePath: "Tests/New.cs"),
            MakeResult("LatestRun", testMemberId: 42, filePath: "Tests/New.cs"),
        };

        var result = await service.ScoreAsync("run-1", history, ValidConfig());

        Assert.Equal("LatestRun", result.TestName);
        Assert.Equal(42, result.TestMemberId);
        Assert.Equal("Tests/New.cs", result.FilePath);
    }

    // ─── Infrastructure ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a <see cref="FlakinessScoringService"/> backed by five
    /// <see cref="FixedScoreProvider"/> instances — one per factor kind — all returning
    /// <paramref name="providerScore"/>.
    /// </summary>
    private static FlakinessScoringService BuildService(double providerScore) =>
        new([
            new FixedScoreProvider(FlakinessFactorKind.OutcomeVariance,  providerScore),
            new FixedScoreProvider(FlakinessFactorKind.RerunInstability, providerScore),
            new FixedScoreProvider(FlakinessFactorKind.DurationVariance, providerScore),
            new FixedScoreProvider(FlakinessFactorKind.FailureSignature, providerScore),
            new FixedScoreProvider(FlakinessFactorKind.EnvironmentSignal, providerScore),
        ]);

    private static FlakyTestDetectionConfig ValidConfig() => new()
    {
        Enabled = true,
        MinimumExecutions = 3,
        HistoryWindowRuns = 20,
        RerunFailedTests = false,
        RerunCount = 2,
        FlakyScoreThreshold = 60.0,
        Weights = new FlakinessWeightsConfig
        {
            OutcomeVariance  = 0.35,
            RerunInstability = 0.25,
            DurationVariance = 0.15,
            FailureSignature = 0.15,
            EnvironmentSignal = 0.10
        }
    };

    private static IReadOnlyList<TestExecutionResultModel> MakeHistory(string testName, int count) =>
        Enumerable.Range(0, count).Select(_ => MakeResult(testName)).ToList();

    private static TestExecutionResultModel MakeResult(
        string testName,
        int? testMemberId = 1,
        string filePath = "Tests/Test.cs",
        string outcome = "Passed") =>
        new()
        {
            TestName = testName,
            TestMemberId = testMemberId,
            FilePath = filePath,
            Outcome = outcome,
            RunId = "run-1"
        };

    /// <summary>
    /// Test double for <see cref="IFlakinessFactorProvider"/>. Always returns
    /// <paramref name="score"/> (optionally with <paramref name="evidence"/>) regardless
    /// of the history passed in.
    /// </summary>
    private sealed class FixedScoreProvider(
        FlakinessFactorKind kind,
        double score,
        string evidence = "")
        : IFlakinessFactorProvider
    {
        public FlakinessFactorKind Factor => kind;

        public Task<FlakinessFactorScore> GetScoreAsync(
            IReadOnlyList<TestExecutionResultModel> history,
            CancellationToken ct = default)
            => Task.FromResult(new FlakinessFactorScore(kind, score, evidence));
    }
}
