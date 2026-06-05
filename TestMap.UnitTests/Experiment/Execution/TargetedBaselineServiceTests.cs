using TestMap.Models.Experiment;
using TestMap.Models.Testing;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.Experiment.Execution;

public sealed class TargetedBaselineServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_WithResolvedProjects_RunsTargetedMutationBaseline()
    {
        var buildTestService = new CapturingBuildTestService
        {
            Result = new TestRunModel { DbId = 123 }
        };
        var service = new TargetedBaselineService(buildTestService);
        var candidate = new CandidateMethod { MethodName = "Calculate" };
        var context = MakeContext();

        var result = await service.RunAsync(9, candidate, context);

        Assert.True(result.Ran);
        Assert.Equal(123, result.TestRunId);
        Assert.NotNull(buildTestService.Request);
        Assert.Equal(BuildTestRunMode.Iteration, buildTestService.Request.Mode);
        Assert.Equal("tests/Tests.csproj", buildTestService.Request.TargetProjectPath);
        Assert.Equal("src/App.csproj", buildTestService.Request.MutationSourceProjectPath);
        Assert.Equal("net10.0", buildTestService.Request.TargetFramework);
        Assert.Equal("Calculate", buildTestService.Request.CoveredMethodName);
        Assert.Equal(9, buildTestService.Request.ExperimentRunId);
        Assert.True(buildTestService.Request.IsMutationBaseline);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_WithoutTestProjectPath_SkipsBaseline()
    {
        var buildTestService = new CapturingBuildTestService();
        var service = new TargetedBaselineService(buildTestService);

        var result = await service.RunAsync(
            9,
            new CandidateMethod { MethodName = "Calculate" },
            MakeContext(testProjectPath: ""));

        Assert.False(result.Ran);
        Assert.Null(result.TestRunId);
        Assert.Contains("test project path", result.SkipReason);
        Assert.Null(buildTestService.Request);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_WithoutSourceProjectPath_SkipsBaseline()
    {
        var buildTestService = new CapturingBuildTestService();
        var service = new TargetedBaselineService(buildTestService);

        var result = await service.RunAsync(
            9,
            new CandidateMethod { MethodName = "Calculate" },
            MakeContext(sourceProjectPath: ""));

        Assert.False(result.Ran);
        Assert.Null(result.TestRunId);
        Assert.Contains("source project path", result.SkipReason);
        Assert.Null(buildTestService.Request);
    }

    private static CandidateMethodContext MakeContext(
        string testProjectPath = "tests/Tests.csproj",
        string sourceProjectPath = "src/App.csproj") => new()
    {
        Method = new CandidateMethod
        {
            Id = 1,
            MemberId = 10,
            MethodName = "Calculate",
            Signature = "int Calculate()"
        },
        MethodSignature = "int Calculate()",
        ContainingClass = "Calculator",
        TestNamespace = "Tests",
        TestClassName = "CalculatorTests",
        TestFilePath = "tests/CalculatorTests.cs",
        SourceFilePath = "src/Calculator.cs",
        SourceLocation = new CandidateSourceLocation(),
        SourceProjectPath = sourceProjectPath,
        TestProjectPath = testProjectPath,
        TargetBuildFramework = "net10.0",
        SolutionFilePath = "App.sln",
        ExampleTest = string.Empty,
        ExampleTestMetadataSummary = string.Empty,
        ProjectTestMetadataSummary = string.Empty,
        TestClass = string.Empty,
        TestFileContents = string.Empty,
        TestSupportContext = string.Empty,
        TestFramework = "xUnit",
        TestDependencies = string.Empty,
        CoverageGapSummary = string.Empty
    };

    private sealed class CapturingBuildTestService : IBuildTestService
    {
        public BuildTestRunRequest? Request { get; private set; }
        public TestRunModel Result { get; init; } = new();

        public Task<TestRunModel> BuildTestAsync(BuildTestRunRequest request)
        {
            Request = request;
            return Task.FromResult(Result);
        }
    }
}
