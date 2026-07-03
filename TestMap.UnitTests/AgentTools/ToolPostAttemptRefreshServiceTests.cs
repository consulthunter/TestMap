using TestMap.App;
using TestMap.Models;
using TestMap.Models.AgentTools;
using TestMap.Models.Code;
using TestMap.Models.Experiment;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="ToolPostAttemptRefreshService"/> covering solution-id resolution
/// from project metadata and the refresh call path.
/// </summary>
public sealed class ToolPostAttemptRefreshServiceTests
{
    // Use fully-resolved temp paths so Path.GetFullPath normalization is a no-op.
    private static readonly string SrcCsproj =
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "testmap-unit", "Src", "Src.csproj"));
    private static readonly string TestCsproj =
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "testmap-unit", "Src.Tests", "Src.Tests.csproj"));

    private static ProjectContext MakeContext(
        IEnumerable<CSharpProjectModel>? projects = null,
        IEnumerable<CSharpSolutionModel>? solutions = null)
    {
        var model = new ProjectModel();
        if (projects != null) model.Projects.AddRange(projects);
        if (solutions != null) model.Solutions.AddRange(solutions);
        return new ProjectContext(model);
    }

    private static CandidateMethodContext MakeMethodContext(string? sourceProjectPath = null) =>
        new()
        {
            Method = new CandidateMethod { Id = 1, MemberId = 1, MethodName = "Foo" },
            MethodSignature = "void Foo()",
            ContainingClass = "MyClass",
            TestNamespace = "Tests",
            TestClassName = "MyClassTests",
            TestFilePath = TestCsproj,
            SourceFilePath = SrcCsproj,
            SourceLocation = new CandidateSourceLocation(),
            SourceProjectPath = sourceProjectPath ?? SrcCsproj,
            TestProjectPath = TestCsproj,
            TargetBuildFramework = "net10.0",
            SolutionFilePath = string.Empty,
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = string.Empty,
            TestFramework = "xunit",
            TestDependencies = string.Empty,
            CoverageGapSummary = string.Empty
        };

    private static ToolAttempt MakeAttempt() =>
        new() { Id = 1, ToolId = "codex", RunStatus = ToolRunStatus.Completed };

    /// <summary>
    /// Returns Refreshed=false when no matching project or solution can be found for
    /// the context's SourceProjectPath.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_NoMatchingProjectOrSolution_ReturnsNotRefreshed()
    {
        var context = MakeContext(); // empty projects + solutions
        var refreshService = new MockSourceTestMappingRefreshService();
        var service = new ToolPostAttemptRefreshService(context, refreshService);

        var result = await service.RefreshAsync(MakeMethodContext(), MakeAttempt());

        Assert.False(result.Refreshed);
        Assert.False(string.IsNullOrWhiteSpace(result.SkipReason));
        Assert.Equal(0, refreshService.CallCount);
    }

    /// <summary>
    /// Resolves solution ID from a matching CSharpProjectModel and calls RefreshForProjectAsync.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_ProjectWithSolutionId_CallsRefreshWithCorrectSolutionId()
    {
        var project = new CSharpProjectModel([], [], [], solutionId: 7, filePath: SrcCsproj);
        var context = MakeContext(projects: [project]);
        var refreshService = new MockSourceTestMappingRefreshService();
        var service = new ToolPostAttemptRefreshService(context, refreshService);

        var result = await service.RefreshAsync(MakeMethodContext(), MakeAttempt());

        Assert.True(result.Refreshed);
        Assert.Equal(7, result.SolutionId);
        Assert.Equal(1, refreshService.CallCount);
        Assert.Equal(7, refreshService.LastSolutionId);
    }

    /// <summary>
    /// Falls back to searching CSharpSolutionModel.Projects when no project has SolutionId set,
    /// and returns the matching solution's ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_SolutionContainsProjectPath_ResolvesSolutionId()
    {
        // Project exists but has SolutionId = 0 (not set via project).
        var project = new CSharpProjectModel([], [], [], solutionId: 0, filePath: SrcCsproj);
        // Solution with Id=3 lists the source project path.
        var solution = new CSharpSolutionModel([SrcCsproj], id: 3);
        var context = MakeContext(projects: [project], solutions: [solution]);
        var refreshService = new MockSourceTestMappingRefreshService();
        var service = new ToolPostAttemptRefreshService(context, refreshService);

        var result = await service.RefreshAsync(MakeMethodContext(), MakeAttempt());

        Assert.True(result.Refreshed);
        Assert.Equal(3, result.SolutionId);
        Assert.Equal(1, refreshService.CallCount);
    }

    /// <summary>
    /// Returns Refreshed=false (no exception) when SourceProjectPath is empty.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_EmptySourceProjectPath_ReturnsNotRefreshed()
    {
        var context = MakeContext();
        var refreshService = new MockSourceTestMappingRefreshService();
        var service = new ToolPostAttemptRefreshService(context, refreshService);

        var result = await service.RefreshAsync(
            MakeMethodContext(sourceProjectPath: string.Empty),
            MakeAttempt());

        Assert.False(result.Refreshed);
        Assert.Equal(0, refreshService.CallCount);
    }

    // ─── Test helpers ─────────────────────────────────────────────────────────

    private sealed class MockSourceTestMappingRefreshService : ISourceTestMappingRefreshService
    {
        public int CallCount { get; private set; }
        public int LastSolutionId { get; private set; }

        public Task RefreshForProjectAsync(int projectId, int solutionId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSolutionId = solutionId;
            return Task.CompletedTask;
        }
    }
}
