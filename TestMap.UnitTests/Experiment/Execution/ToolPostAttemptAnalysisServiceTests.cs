using TestMap.App;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.StaticAnalysis.Enrichment;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.Experiment.Execution;

/// <summary>
/// Tests for <see cref="ToolPostAttemptAnalysisService"/>.
/// Verifies skip conditions, delegation to the three analysis services, and topological ordering.
/// </summary>
public sealed class ToolPostAttemptAnalysisServiceTests
{
    // ─── Skip conditions ─────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_EmptyTestProjectPath_ReturnsNotAnalyzed()
    {
        /// Arrange: method context has no test project path.
        var (svc, analyze, metrics, smells) = MakeService(dbId: 1);

        /// Act
        var result = await svc.AnalyzeAsync(MakeContext(testProjectPath: string.Empty));

        /// Assert
        Assert.False(result.Analyzed);
        Assert.NotEmpty(result.SkipReason);
        Assert.Empty(analyze.Calls);
        Assert.Empty(metrics.Calls);
        Assert.Empty(smells.Calls);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_TestProjectNotInContext_ReturnsNotAnalyzed()
    {
        /// Arrange: project context has no projects — path won't match.
        var (svc, analyze, metrics, smells) = MakeService(dbId: 1, projects: []);

        /// Act
        var result = await svc.AnalyzeAsync(MakeContext(testProjectPath: "tests/Tests.csproj"));

        /// Assert
        Assert.False(result.Analyzed);
        Assert.Contains("Tests.csproj", result.SkipReason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(analyze.Calls);
        Assert.Empty(metrics.Calls);
        Assert.Empty(smells.Calls);
    }

    // ─── Delegation ──────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_ValidProject_AnalyzesAllProjectsInContext()
    {
        /// Arrange: two projects in context; test project is the second.
        var testProjectPath = Path.GetFullPath("tests/Tests.csproj");
        var srcProjectPath  = Path.GetFullPath("src/App.csproj");
        var projects = new List<CSharpProjectModel>
        {
            MakeProject(srcProjectPath),
            MakeProject(testProjectPath)
        };
        var (svc, analyze, _, _) = MakeService(dbId: 1, projects: projects);

        /// Act
        var result = await svc.AnalyzeAsync(MakeContext(testProjectPath));

        /// Assert: AnalyzeProjectAsync called once per project.
        Assert.True(result.Analyzed);
        Assert.Equal(2, analyze.Calls.Count);
        Assert.Contains(analyze.Calls, c => c.FilePath == srcProjectPath);
        Assert.Contains(analyze.Calls, c => c.FilePath == testProjectPath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_ValidProject_CallsCodeMetricsForTestProject()
    {
        /// Arrange
        var testProjectPath = Path.GetFullPath("tests/Tests.csproj");
        var (svc, _, metrics, _) = MakeService(dbId: 1,
            projects: [MakeProject(testProjectPath)]);

        /// Act
        var result = await svc.AnalyzeAsync(MakeContext(testProjectPath));

        /// Assert: code metrics collected once, for the test project.
        Assert.True(result.Analyzed);
        var call = Assert.Single(metrics.Calls);
        Assert.Equal(testProjectPath, call.FilePath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_ValidProject_CallsTestSmellsWithProjectPathAndDbId()
    {
        /// Arrange
        var testProjectPath = Path.GetFullPath("tests/Tests.csproj");
        var (svc, _, _, smells) = MakeService(dbId: 42,
            projects: [MakeProject(testProjectPath)]);

        /// Act
        var result = await svc.AnalyzeAsync(MakeContext(testProjectPath));

        /// Assert: smell collection called with the exact path and project DB id.
        Assert.True(result.Analyzed);
        var call = Assert.Single(smells.Calls);
        Assert.Equal(testProjectPath, call.Path);
        Assert.Equal(42, call.ProjectId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_DbIdZero_SkipsTestSmellCollection()
    {
        /// Arrange: project has no persisted DB id yet.
        var testProjectPath = Path.GetFullPath("tests/Tests.csproj");
        var (svc, _, _, smells) = MakeService(dbId: 0,
            projects: [MakeProject(testProjectPath)]);

        /// Act
        var result = await svc.AnalyzeAsync(MakeContext(testProjectPath));

        /// Assert: analysis succeeds but smells are not collected.
        Assert.True(result.Analyzed);
        Assert.Empty(smells.Calls);
    }

    // ─── Topological sort ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public void TopologicallySorted_DependencyBeforeDependent_PreservesOrder()
    {
        /// Arrange: App.csproj references Core.csproj.
        var corePath = Path.GetFullPath("src/Core.csproj");
        var appPath  = Path.GetFullPath("src/App.csproj");
        var core = MakeProject(corePath);
        var app  = MakeProject(appPath, references: [corePath]);

        /// Act: list with dependent first to confirm sort re-orders.
        var sorted = ToolPostAttemptAnalysisService.TopologicallySorted([app, core]);

        /// Assert: Core (dependency) appears before App (dependent).
        var coreIndex = sorted.ToList().FindIndex(p => p.FilePath == corePath);
        var appIndex  = sorted.ToList().FindIndex(p => p.FilePath == appPath);
        Assert.True(coreIndex < appIndex, "dependency must precede dependent in topological order");
    }

    // ─── Infrastructure ──────────────────────────────────────────────────────

    private static (
        ToolPostAttemptAnalysisService Service,
        CapturingAnalyzeProjectService Analyze,
        CapturingCodeMetricsService Metrics,
        CapturingTestSmellService Smells)
        MakeService(int dbId, List<CSharpProjectModel>? projects = null)
    {
        var project = new ProjectModel { DbId = dbId };
        if (projects != null)
            foreach (var p in projects)
                project.Projects.Add(p);

        var analyze = new CapturingAnalyzeProjectService();
        var metrics = new CapturingCodeMetricsService();
        var smells  = new CapturingTestSmellService();
        var svc = new ToolPostAttemptAnalysisService(
            new ProjectContext(project),
            analyze,
            metrics,
            smells);
        return (svc, analyze, metrics, smells);
    }

    private static CandidateMethodContext MakeContext(string testProjectPath) => new()
    {
        Method = new CandidateMethod { Id = 1, MemberId = 10, MethodName = "M", Signature = "void M()" },
        MethodSignature          = "void M()",
        ContainingClass          = "C",
        TestNamespace            = "Tests",
        TestClassName            = "CTests",
        TestFilePath             = "tests/CTests.cs",
        SourceFilePath           = "src/C.cs",
        SourceLocation           = new CandidateSourceLocation(),
        SourceProjectPath        = Path.GetFullPath("src/App.csproj"),
        TestProjectPath          = testProjectPath,
        TargetBuildFramework     = "net10.0",
        SolutionFilePath         = "App.sln",
        ExampleTest              = string.Empty,
        ExampleTestMetadataSummary = string.Empty,
        ProjectTestMetadataSummary = string.Empty,
        TestClass                = string.Empty,
        TestFileContents         = string.Empty,
        TestSupportContext       = string.Empty,
        TestFramework            = "xUnit",
        TestDependencies         = string.Empty,
        CoverageGapSummary       = string.Empty
    };

    private static CSharpProjectModel MakeProject(string filePath, List<string>? references = null) =>
        new(
            projectReferences: references ?? [],
            documentFilePaths: [],
            buildTargets: [],
            filePath: filePath);

    // ─── Test doubles ─────────────────────────────────────────────────────────

    private sealed class CapturingAnalyzeProjectService : IAnalyzeProjectService
    {
        public List<CSharpProjectModel> Calls { get; } = [];

        public Task AnalyzeProjectAsync(
            CSharpProjectModel analysisProject,
            Dictionary<string, int>? sharedMemberIds = null)
        {
            Calls.Add(analysisProject);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingCodeMetricsService : ICodeMetricsService
    {
        public List<CSharpProjectModel> Calls { get; } = [];

        public Task CollectCodeMetricsAsync(
            CSharpSolutionModel analysisSolution,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CollectCodeMetricsAsync(
            CSharpProjectModel analysisProject,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(analysisProject);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingTestSmellService : ITestSmellService
    {
        public List<(string Path, int ProjectId)> Calls { get; } = [];

        public Task CollectAsync(
            string solutionOrProjectPath,
            int projectId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((solutionOrProjectPath, projectId));
            return Task.CompletedTask;
        }
    }
}
