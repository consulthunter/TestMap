using TestMap.App;
using TestMap.Models.Code;
using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public sealed class TestBootstrapService : ITestBootstrapService
{
    private readonly ProjectContext _context;
    private readonly ITestBootstrapDetectionService _detectionService;
    private readonly ITestProjectBootstrapService _projectBootstrapService;
    private readonly ITestProjectScaffoldingService _scaffoldingService;

    public TestBootstrapService(
        ProjectContext context,
        ITestBootstrapDetectionService detectionService,
        ITestProjectBootstrapService projectBootstrapService,
        ITestProjectScaffoldingService scaffoldingService)
    {
        _context = context;
        _detectionService = detectionService;
        _projectBootstrapService = projectBootstrapService;
        _scaffoldingService = scaffoldingService;
    }

    public Task<TestBootstrapPlan> PlanAsync(CancellationToken cancellationToken = default)
    {
        return BuildPlanAsync(false, cancellationToken);
    }

    public async Task<TestBootstrapPlan> EnsureBootstrapAsync(CancellationToken cancellationToken = default)
    {
        if (_context.TestBootstrapState != null)
            return new TestBootstrapPlan
            {
                Detection = new TestBootstrapDetectionResult
                {
                    ShouldBootstrap = true,
                    Reason = "Bootstrap test infrastructure is already active.",
                    HasTestProjects = true,
                    HasDiscoveredTestMembers = false
                },
                ProjectBootstrap = new TestProjectBootstrapResult
                {
                    Success = true,
                    TestProjectName = _context.TestBootstrapState.TestProjectName,
                    TestProjectPath = _context.TestBootstrapState.TestProjectPath,
                    PlannedOperations = Array.Empty<string>()
                },
                InitialScaffold = new TestProjectScaffoldingResult
                {
                    Success = true,
                    TestClassName = _context.TestBootstrapState.TestClassName,
                    TestFilePath = _context.TestBootstrapState.TestFilePath,
                    Framework = _context.TestBootstrapState.Framework,
                    ScaffoldPreview = string.Empty
                }
            };

        return await BuildPlanAsync(true, cancellationToken);
    }

    private async Task<TestBootstrapPlan> BuildPlanAsync(bool applyChanges, CancellationToken cancellationToken)
    {
        var detection = await _detectionService.DetectAsync(cancellationToken);
        if (!detection.ShouldBootstrap) return new TestBootstrapPlan { Detection = detection };

        var sourceProject = _context.Project.Projects
            .Where(x => !x.BuildMetadata.IsTestProject)
            .OrderByDescending(x => x.DocumentFilePaths.Count)
            .ThenBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (sourceProject == null) return new TestBootstrapPlan { Detection = detection };

        var bootstrapConfig = _context.Project.Config.TestingConfig.GenerationConfig.Bootstrap;
        var request = new TestBootstrapRequest
        {
            SourceProjectPath = sourceProject.FilePath,
            SolutionPath = _context.Project.Solutions.FirstOrDefault()?.FilePath,
            Framework = bootstrapConfig.DefaultFramework,
            TestProjectSuffix = bootstrapConfig.TestProjectSuffix,
            AddCoverletCollector = bootstrapConfig.AddCoverletCollector,
            InitialCandidateLimit = bootstrapConfig.InitialCandidateLimit
        };

        var projectBootstrap = await _projectBootstrapService.CreateTestProjectAsync(
            request,
            applyChanges,
            cancellationToken);
        var initialScaffold = await _scaffoldingService.ScaffoldAsync(
            request,
            applyChanges,
            cancellationToken);

        if (applyChanges)
        {
            var targetFramework = ResolveTargetFramework(sourceProject);
            var dependencies =
                BuildDependencies(bootstrapConfig.DefaultFramework, bootstrapConfig.AddCoverletCollector);
            _context.TestBootstrapState = new TestBootstrapRuntimeState
            {
                SourceProjectPath = sourceProject.FilePath,
                TestProjectPath = projectBootstrap.TestProjectPath,
                TestProjectName = projectBootstrap.TestProjectName,
                TestClassName = initialScaffold.TestClassName,
                TestFilePath = initialScaffold.TestFilePath,
                Framework = initialScaffold.Framework,
                TargetFramework = targetFramework,
                Dependencies = dependencies
            };

            AddBootstrapProjectToContext(sourceProject, projectBootstrap, initialScaffold, targetFramework);
        }

        return new TestBootstrapPlan
        {
            Detection = detection,
            ProjectBootstrap = projectBootstrap,
            InitialScaffold = initialScaffold
        };
    }

    private void AddBootstrapProjectToContext(
        CSharpProjectModel sourceProject,
        TestProjectBootstrapResult projectBootstrap,
        TestProjectScaffoldingResult initialScaffold,
        string targetFramework)
    {
        if (_context.Project.Projects.Any(x =>
                string.Equals(Path.GetFullPath(x.FilePath), Path.GetFullPath(projectBootstrap.TestProjectPath),
                    StringComparison.OrdinalIgnoreCase)))
            return;

        var testProject = new CSharpProjectModel(
            [sourceProject.FilePath],
            [initialScaffold.TestFilePath],
            [targetFramework],
            new ProjectBuildMetadataModel
            {
                BuildTargets = [targetFramework],
                DefaultBuildTarget = targetFramework,
                IsTestProject = true,
                CoverageCollector = sourceProject.BuildMetadata.CoverageCollector,
                Notes =
                    $"Bootstrapped {_context.Project.Config.TestingConfig.GenerationConfig.Bootstrap.DefaultFramework} test project."
            },
            filePath: projectBootstrap.TestProjectPath,
            defaultBuildTarget: targetFramework);

        _context.Project.Projects.Add(testProject);

        var solution = _context.Project.Solutions.FirstOrDefault();
        if (solution != null &&
            !solution.Projects.Contains(projectBootstrap.TestProjectPath, StringComparer.OrdinalIgnoreCase))
            solution.Projects.Add(projectBootstrap.TestProjectPath);
    }

    private static string ResolveTargetFramework(CSharpProjectModel sourceProject)
    {
        if (!string.IsNullOrWhiteSpace(sourceProject.BuildMetadata.DefaultBuildTarget))
            return sourceProject.BuildMetadata.DefaultBuildTarget;

        if (!string.IsNullOrWhiteSpace(sourceProject.DefaultBuildTarget)) return sourceProject.DefaultBuildTarget;

        return sourceProject.BuildTargets.FirstOrDefault() ?? "net10.0";
    }

    private static string BuildDependencies(string framework, bool includeCoverletCollector)
    {
        var lines = framework switch
        {
            "NUnit" => new List<string> { "using NUnit.Framework;" },
            "MSTest" => new List<string> { "using Microsoft.VisualStudio.TestTools.UnitTesting;" },
            _ => new List<string> { "using Xunit;" }
        };

        if (includeCoverletCollector) lines.Add("// coverlet.collector configured in test project");

        return string.Join(Environment.NewLine, lines);
    }
}