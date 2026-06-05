using TestMap.App;
using TestMap.Models;
using TestMap.Models.AgentTools;
using TestMap.Models.Code;
using TestMap.Models.Experiment;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.Experiment.Execution;

public sealed class ToolPostAttemptRefreshServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_WhenSourceProjectMatchesKnownProject_RefreshesMappedSolution()
    {
        var sourceProjectPath = Path.GetFullPath(Path.Combine("repo", "src", "App.csproj"));
        var project = new ProjectModel(directoryPath: Path.GetDirectoryName(sourceProjectPath) ?? string.Empty)
        {
            DbId = 9
        };
        project.Projects.Add(new CSharpProjectModel(
            projectReferences: [],
            documentFilePaths: [],
            buildTargets: [],
            id: 3,
            solutionId: 7,
            filePath: sourceProjectPath));
        var refreshService = new CapturingRefreshService();
        var service = new ToolPostAttemptRefreshService(new ProjectContext(project), refreshService);

        var result = await service.RefreshAsync(
            MakeContext(sourceProjectPath),
            new ToolAttempt { Id = 77 });

        Assert.True(result.Refreshed);
        Assert.Equal(7, result.SolutionId);
        var call = Assert.Single(refreshService.Calls);
        Assert.Equal(9, call.ProjectId);
        Assert.Equal(7, call.SolutionId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_WhenProjectModelHasNoSolutionId_FallsBackToSolutionProjectList()
    {
        var sourceProjectPath = Path.GetFullPath(Path.Combine("repo", "src", "App.csproj"));
        var project = new ProjectModel { DbId = 9 };
        project.Projects.Add(new CSharpProjectModel(
            projectReferences: [],
            documentFilePaths: [],
            buildTargets: [],
            id: 3,
            solutionId: 0,
            filePath: sourceProjectPath));
        project.Solutions.Add(new CSharpSolutionModel(
            projects: [sourceProjectPath],
            id: 11,
            projectId: 9,
            filePath: Path.GetFullPath(Path.Combine("repo", "App.sln"))));
        var refreshService = new CapturingRefreshService();
        var service = new ToolPostAttemptRefreshService(new ProjectContext(project), refreshService);

        var result = await service.RefreshAsync(
            MakeContext(sourceProjectPath),
            new ToolAttempt { Id = 77 });

        Assert.True(result.Refreshed);
        Assert.Equal(11, result.SolutionId);
        var call = Assert.Single(refreshService.Calls);
        Assert.Equal(9, call.ProjectId);
        Assert.Equal(11, call.SolutionId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_WhenSolutionCannotBeResolved_SkipsRefresh()
    {
        var refreshService = new CapturingRefreshService();
        var service = new ToolPostAttemptRefreshService(
            new ProjectContext(new ProjectModel { DbId = 9 }),
            refreshService);

        var result = await service.RefreshAsync(
            MakeContext("src/Missing.csproj"),
            new ToolAttempt { Id = 77 });

        Assert.False(result.Refreshed);
        Assert.Null(result.SolutionId);
        Assert.Contains("No solution id", result.SkipReason);
        Assert.Empty(refreshService.Calls);
    }

    private static CandidateMethodContext MakeContext(string sourceProjectPath) => new()
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
        TestProjectPath = "tests/Tests.csproj",
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

    private sealed class CapturingRefreshService : ISourceTestMappingRefreshService
    {
        public List<(int ProjectId, int SolutionId)> Calls { get; } = [];

        public Task RefreshForProjectAsync(
            int projectId,
            int solutionId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((projectId, solutionId));
            return Task.CompletedTask;
        }
    }
}
