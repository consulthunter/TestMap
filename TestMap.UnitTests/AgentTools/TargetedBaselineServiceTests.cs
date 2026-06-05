using TestMap.Models.Experiment;
using TestMap.Models.Testing;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="TargetedBaselineService"/> covering skip conditions and the happy path.
/// Uses a mock <see cref="IBuildTestService"/> to avoid real Docker/test execution.
/// </summary>
public sealed class TargetedBaselineServiceTests
{
    private static CandidateMethod MakeCandidate() =>
        new()
        {
            Id = 1,
            MemberId = 10,
            MethodName = "Calculate"
        };

    private static CandidateMethodContext MakeContext(
        string testProjectPath = "/repo/Tests",
        string sourceProjectPath = "/repo/Src") =>
        new()
        {
            Method = new CandidateMethod { Id = 1, MemberId = 10, MethodName = "Calculate" },
            MethodSignature = "public int Calculate(int x)",
            ContainingClass = "Calculator",
            TestNamespace = "Tests",
            TestClassName = "CalculatorTests",
            TestFilePath = "/repo/Tests/CalculatorTests.cs",
            SourceFilePath = "/repo/Src/Calculator.cs",
            SourceLocation = new CandidateSourceLocation { StartLine = 10, EndLine = 20 },
            SourceProjectPath = sourceProjectPath,
            TestProjectPath = testProjectPath,
            TargetBuildFramework = "net10.0",
            SolutionFilePath = "/repo/TestRepo.sln",
            ExampleTest = "// example",
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = string.Empty,
            TestFramework = "xunit",
            TestDependencies = string.Empty,
            CoverageGapSummary = string.Empty
        };

    /// <summary>
    /// Returns Ran=false with a skip reason when TestProjectPath is empty.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_EmptyTestProjectPath_ReturnsSkipped()
    {
        var service = new TargetedBaselineService(new MockBuildTestService());

        var result = await service.RunAsync(
            experimentRunId: 1,
            candidate: MakeCandidate(),
            methodContext: MakeContext(testProjectPath: string.Empty));

        Assert.False(result.Ran);
        Assert.False(string.IsNullOrWhiteSpace(result.SkipReason));
        Assert.Null(result.TestRunId);
    }

    /// <summary>
    /// Returns Ran=false with a skip reason when SourceProjectPath is empty.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_EmptySourceProjectPath_ReturnsSkipped()
    {
        var service = new TargetedBaselineService(new MockBuildTestService());

        var result = await service.RunAsync(
            experimentRunId: 1,
            candidate: MakeCandidate(),
            methodContext: MakeContext(sourceProjectPath: string.Empty));

        Assert.False(result.Ran);
        Assert.False(string.IsNullOrWhiteSpace(result.SkipReason));
        Assert.Null(result.TestRunId);
    }

    /// <summary>
    /// When BuildTestAsync returns a persisted run (DbId > 0), Ran=true and TestRunId is populated.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_SuccessfulBuild_ReturnsRanTrueWithTestRunId()
    {
        var service = new TargetedBaselineService(new MockBuildTestService(dbId: 42));

        var result = await service.RunAsync(
            experimentRunId: 1,
            candidate: MakeCandidate(),
            methodContext: MakeContext());

        Assert.True(result.Ran);
        Assert.Equal(42, result.TestRunId);
        Assert.True(string.IsNullOrWhiteSpace(result.SkipReason));
    }

    /// <summary>
    /// When BuildTestAsync returns DbId=0 (not persisted), Ran=true but TestRunId is null.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_BuildNotPersisted_ReturnsRanTrueWithNullTestRunId()
    {
        var service = new TargetedBaselineService(new MockBuildTestService(dbId: 0));

        var result = await service.RunAsync(
            experimentRunId: 1,
            candidate: MakeCandidate(),
            methodContext: MakeContext());

        Assert.True(result.Ran);
        Assert.Null(result.TestRunId);
    }

    /// <summary>
    /// The request passed to BuildTestAsync uses the correct target and source project paths.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_PassesCorrectProjectPaths_ToBuildTestService()
    {
        var mock = new CapturingMockBuildTestService();
        var service = new TargetedBaselineService(mock);
        var context = MakeContext(
            testProjectPath: "/repo/My.Tests",
            sourceProjectPath: "/repo/My.Src");

        await service.RunAsync(
            experimentRunId: 7,
            candidate: MakeCandidate(),
            methodContext: context);

        Assert.NotNull(mock.CapturedRequest);
        Assert.Equal("/repo/My.Tests", mock.CapturedRequest!.TargetProjectPath);
        Assert.Equal("/repo/My.Src", mock.CapturedRequest.MutationSourceProjectPath);
        Assert.True(mock.CapturedRequest.IsMutationBaseline);
        Assert.Equal(7, mock.CapturedRequest.ExperimentRunId);
    }

    // ─── Test helpers ─────────────────────────────────────────────────────────

    private sealed class MockBuildTestService : IBuildTestService
    {
        private readonly int _dbId;
        public MockBuildTestService(int dbId = 1) => _dbId = dbId;

        public Task<TestRunModel> BuildTestAsync(BuildTestRunRequest request) =>
            Task.FromResult(new TestRunModel { DbId = _dbId });
    }

    private sealed class CapturingMockBuildTestService : IBuildTestService
    {
        public BuildTestRunRequest? CapturedRequest { get; private set; }

        public Task<TestRunModel> BuildTestAsync(BuildTestRunRequest request)
        {
            CapturedRequest = request;
            return Task.FromResult(new TestRunModel { DbId = 1 });
        }
    }
}
