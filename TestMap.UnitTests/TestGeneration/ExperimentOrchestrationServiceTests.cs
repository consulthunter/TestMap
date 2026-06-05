using LibGit2Sharp;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.AgentTools;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.Workspace;

namespace TestMap.UnitTests.TestGeneration;

public sealed class ExperimentOrchestrationServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    [Fact]
    [Trait("Category", "Unit")]
    public void ShouldRequirePassingExistingTest_ForTestSuiteExpansion_ReturnsFalse()
    {
        var result = ExperimentOrchestrationService.ShouldRequirePassingExistingTest(
            TestGenerationObjective.TestSuiteExpansion);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PrecomputeTargetedBaselinesAsync_RollsBackBaselineArtifactsBeforeLanes()
    {
        var repoPath = CreateRepository();
        var baselineService = new WritingTargetedBaselineService(repoPath, testRunId: 42);
        var service = MakeService(repoPath, baselineService);
        var candidate = new CandidateMethod
        {
            Id = 7,
            MemberId = 11,
            MethodName = "Start"
        };
        var context = MakeContext(candidate, repoPath);
        var plan = new AgentToolAvailabilityPlan
        {
            Decisions =
            [
                new AgentToolAvailabilityDecision
                {
                    Tool = new ExperimentToolConfig { Id = "codex" },
                    Availability = new ToolAvailabilityResult { ToolId = "codex", IsAvailable = true },
                    ShouldExecute = true
                }
            ]
        };

        var result = await service.PrecomputeTargetedBaselinesAsync(
            experimentRunId: 3,
            [candidate],
            new Dictionary<int, CandidateMethodContext> { [candidate.MemberId] = context },
            plan,
            CancellationToken.None);

        Assert.Equal(1, baselineService.CallCount);
        Assert.True(result[candidate.Id].Ran);
        Assert.Equal(42, result[candidate.Id].TestRunId);
        Assert.False(Directory.Exists(Path.Combine(repoPath, "coverage")));
        Assert.False(Directory.Exists(Path.Combine(repoPath, "TestMap-Example", "obj")));
        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(repoPath, "tracked.txt")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PrecomputeTargetedBaselinesAsync_DoesNotRunWithoutExecutableTools()
    {
        var repoPath = CreateRepository();
        var baselineService = new WritingTargetedBaselineService(repoPath, testRunId: 42);
        var service = MakeService(repoPath, baselineService);
        var candidate = new CandidateMethod
        {
            Id = 7,
            MemberId = 11,
            MethodName = "Start"
        };

        var result = await service.PrecomputeTargetedBaselinesAsync(
            experimentRunId: 3,
            [candidate],
            new Dictionary<int, CandidateMethodContext> { [candidate.MemberId] = MakeContext(candidate, repoPath) },
            new AgentToolAvailabilityPlan(),
            CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(0, baselineService.CallCount);
    }

    private static ExperimentOrchestrationService MakeService(
        string repoPath,
        ITargetedBaselineService targetedBaselineService)
    {
        var project = new ProjectModel(directoryPath: repoPath);
        var context = new ProjectContext(project);

        return new ExperimentOrchestrationService(
            context,
            new TestMapConfig(),
            methodSelection: null!,
            pipeline: null!,
            experimentRunRepo: null!,
            workItemRepo: null!,
            candidateMethodRepo: null!,
            attemptRepo: null!,
            stepRepo: null!,
            executionRepo: null!,
            riskScoreRepo: null!,
            generatedTestExecutionService: null!,
            generationValidationService: null!,
            generationClassificationService: null!,
            matrixGenerator: null!,
            budgetExecutor: null!,
            buildTestService: null!,
            resumeService: null!,
            ruleDecisionRecorder: null!,
            resultsWriter: null!,
            artifactCleanupService: null!,
            dbContext: null!,
            dockerToolRunner: null!,
            agentToolEnvironmentResolver: null!,
            toolAttemptRepo: null!,
            taskCardWriter: null!,
            targetedBaselineService,
            toolPostAttemptAnalysisService: null!,
            toolAttemptGeneratedTestService: null!,
            toolPostAttemptMeasurementService: null!,
            generationApproaches: [],
            new RollbackWorkspaceService(context));
    }

    private static CandidateMethodContext MakeContext(CandidateMethod candidate, string repoPath) =>
        new()
        {
            Method = candidate,
            MethodSignature = "void Start(TextReader reader, TextWriter writer)",
            ContainingClass = "Program",
            TestNamespace = "TestMap_Example.Tests",
            TestClassName = "ProgramTest",
            TestFilePath = Path.Combine(repoPath, "TestMap-Example.Tests", "ProgramTest.cs"),
            SourceFilePath = Path.Combine(repoPath, "TestMap-Example", "Program.cs"),
            SourceLocation = new CandidateSourceLocation(),
            SourceProjectPath = Path.Combine(repoPath, "TestMap-Example", "TestMap-Example.csproj"),
            TestProjectPath = Path.Combine(repoPath, "TestMap-Example.Tests", "TestMap-Example.Tests.csproj"),
            TargetBuildFramework = "net8.0",
            SolutionFilePath = Path.Combine(repoPath, "TestMap-Example.sln"),
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = string.Empty,
            TestFramework = "MSTest",
            TestDependencies = string.Empty,
            CoverageGapSummary = string.Empty
        };

    private string CreateRepository()
    {
        var repoPath = Path.Combine(Path.GetTempPath(), $"testmap-experiment-{Guid.NewGuid():N}");
        _directoriesToDelete.Add(repoPath);
        Directory.CreateDirectory(repoPath);
        Repository.Init(repoPath);

        var trackedFile = Path.Combine(repoPath, "tracked.txt");
        File.WriteAllText(trackedFile, "original");

        using var repo = new Repository(repoPath);
        Commands.Stage(repo, trackedFile);

        var signature = new Signature("TestMap", "testmap@example.com", DateTimeOffset.UtcNow);
        repo.Commit("Initial commit", signature, signature);

        return repoPath;
    }

    public void Dispose()
    {
        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
        {
            if (!Directory.Exists(directory)) continue;

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(directory, true);
        }
    }

    private sealed class WritingTargetedBaselineService : ITargetedBaselineService
    {
        private readonly string _repoPath;
        private readonly int _testRunId;

        public WritingTargetedBaselineService(string repoPath, int testRunId)
        {
            _repoPath = repoPath;
            _testRunId = testRunId;
        }

        public int CallCount { get; private set; }

        public async Task<TargetedBaselineResult> RunAsync(
            int experimentRunId,
            CandidateMethod candidate,
            CandidateMethodContext methodContext,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            await File.WriteAllTextAsync(Path.Combine(_repoPath, "tracked.txt"), "baseline changed", cancellationToken);

            var coverageDir = Path.Combine(_repoPath, "coverage");
            Directory.CreateDirectory(coverageDir);
            await File.WriteAllTextAsync(Path.Combine(coverageDir, "baseline.trx"), "coverage", cancellationToken);

            var objDir = Path.Combine(_repoPath, "TestMap-Example", "obj");
            Directory.CreateDirectory(objDir);
            await File.WriteAllTextAsync(Path.Combine(objDir, "project.assets.json"), "{}", cancellationToken);

            return new TargetedBaselineResult { Ran = true, TestRunId = _testRunId };
        }
    }
}
