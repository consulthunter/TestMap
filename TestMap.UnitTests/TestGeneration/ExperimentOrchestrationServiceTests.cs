using LibGit2Sharp;
using System.Security.Cryptography;
using System.Text;
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
using TestMap.Models.Targets;

namespace TestMap.UnitTests.TestGeneration;

public sealed class ExperimentOrchestrationServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyCumulativeTokenUsage_AccumulatesRepairsAndResetsIndependentAttempt()
    {
        int? cumulativeInput = 0;
        int? cumulativeOutput = 0;
        var initial = new GenerationAttempt { InputTokens = 100, OutputTokens = 20 };
        var repair = new GenerationAttempt { IsRepairAttempt = true, InputTokens = 60, OutputTokens = 10 };
        var independent = new GenerationAttempt { InputTokens = 7, OutputTokens = 3 };

        ExperimentOrchestrationService.ApplyCumulativeTokenUsage(
            initial, ref cumulativeInput, ref cumulativeOutput);
        ExperimentOrchestrationService.ApplyCumulativeTokenUsage(
            repair, ref cumulativeInput, ref cumulativeOutput);

        Assert.Equal(160, repair.ChainCumulativeInputTokens);
        Assert.Equal(30, repair.ChainCumulativeOutputTokens);
        Assert.Equal(190, repair.ChainCumulativeTokensUsed);

        ExperimentOrchestrationService.ApplyCumulativeTokenUsage(
            independent, ref cumulativeInput, ref cumulativeOutput);
        Assert.Equal(7, independent.ChainCumulativeInputTokens);
        Assert.Equal(3, independent.ChainCumulativeOutputTokens);
        Assert.Equal(10, independent.ChainCumulativeTokensUsed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyCumulativeTokenUsage_MissingComponentInvalidatesOnlyThatComponent()
    {
        int? cumulativeInput = 100;
        int? cumulativeOutput = 20;
        var repair = new GenerationAttempt
        {
            IsRepairAttempt = true,
            InputTokens = null,
            OutputTokens = 5
        };

        ExperimentOrchestrationService.ApplyCumulativeTokenUsage(
            repair, ref cumulativeInput, ref cumulativeOutput);

        Assert.Null(repair.ChainCumulativeInputTokens);
        Assert.Equal(25, repair.ChainCumulativeOutputTokens);
        Assert.Null(repair.ChainCumulativeTokensUsed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CaptureModifiedFileAsync_AppliedPatch_StoresExactSnapshotAndHash()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"testmap-snapshot-{Guid.NewGuid():N}");
        _directoriesToDelete.Add(directory);
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "WidgetTests.cs");
        var contents = "public class WidgetTests\r\n{\r\n    // exact snapshot\r\n}\r\n";
        await File.WriteAllTextAsync(filePath, contents);
        var attempt = new GenerationAttempt
        {
            TestExecution = new TestMap.Models.Experiment.TestExecution
            {
                PatchApplicationOutcome = "Success"
            }
        };

        await ExperimentOrchestrationService.CaptureModifiedFileAsync(attempt, filePath);

        Assert.Equal(Path.GetFullPath(filePath), attempt.ModifiedFilePath);
        Assert.Equal(contents, attempt.ModifiedFileContents);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contents))).ToLowerInvariant(),
            attempt.ModifiedFileSha256);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CaptureModifiedFileAsync_FailedPatch_DoesNotStoreOriginalFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"testmap-snapshot-{Guid.NewGuid():N}");
        _directoriesToDelete.Add(directory);
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "WidgetTests.cs");
        await File.WriteAllTextAsync(filePath, "original");
        var attempt = new GenerationAttempt
        {
            TestExecution = new TestMap.Models.Experiment.TestExecution
            {
                PatchApplicationOutcome = "MalformedTestMethod"
            }
        };

        await ExperimentOrchestrationService.CaptureModifiedFileAsync(attempt, filePath);

        Assert.Null(attempt.ModifiedFilePath);
        Assert.Null(attempt.ModifiedFileContents);
        Assert.Null(attempt.ModifiedFileSha256);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ShouldRequirePassingExistingTest_ForTestSuiteExpansion_ReturnsTrue()
    {
        var result = ExperimentOrchestrationService.ShouldRequirePassingExistingTest(
            TestGenerationObjective.TestSuiteExpansion);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveReportedAccessStrategy_WithoutResolvedPath_ReturnsUnknown()
    {
        var result = ExperimentOrchestrationService.ResolveReportedAccessStrategy(new SourceMemberTestability
        {
            SourceMemberId = 1,
            Visibility = MemberVisibility.Public,
            TestMappings = [],
            AccessPaths = [],
            EvidenceStatuses = [],
            SetupBindings = []
        });

        Assert.Equal(TestAccessStrategy.Unknown, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveAgentToolBudgetModes_UsesOnlyPassAt1AndPassAt5()
    {
        var config = new ExperimentConfig
        {
            BudgetModes =
            [
                GenerationBudgetMode.PassAt1,
                GenerationBudgetMode.PassAt5,
                GenerationBudgetMode.PassAt1RepairAt5,
                GenerationBudgetMode.PassAt5
            ]
        };

        var result = ExperimentOrchestrationService.ResolveAgentToolBudgetModes(config);

        Assert.Equal(
            [GenerationBudgetMode.PassAt1, GenerationBudgetMode.PassAt5],
            result);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(GenerationBudgetMode.PassAt1, 1)]
    [InlineData(GenerationBudgetMode.PassAt5, 5)]
    [InlineData(GenerationBudgetMode.PassAt1RepairAt5, 1)]
    public void GetAgentToolAttemptCount_MapsSupportedBudgetsToIndependentAttemptCounts(
        GenerationBudgetMode budgetMode,
        int expected)
    {
        var result = ExperimentOrchestrationService.GetAgentToolAttemptCount(budgetMode);

        Assert.Equal(expected, result);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(TestClassification.ValidatedEvidencePositive, true)]
    [InlineData(TestClassification.ValidatedLowImpact, true)]
    [InlineData(TestClassification.ValidatedImpactUnknown, true)]
    [InlineData(TestClassification.FailedEvidencePositive, false)]
    [InlineData(TestClassification.ValidationFailed, false)]
    public void IsValidatedClassification_OnlyAcceptsValidatedOutcomes(
        TestClassification classification,
        bool expected)
    {
        Assert.Equal(expected, ExperimentOrchestrationService.IsValidatedClassification(classification));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PrepareConfiguration_CreateCohort_PreservesExplicitSeriesId()
    {
        var config = new ExperimentConfig
        {
            ExperimentSeriesId = "model-study",
            CandidateCohort = new CandidateCohortConfig
            {
                Id = "cohort-a",
                Mode = CandidateCohortMode.Create,
                RandomSeed = 42
            }
        };

        CandidateCohortService.PrepareConfiguration(config);

        Assert.Equal("model-study", config.ExperimentSeriesId);
        Assert.Equal(42, config.CandidateCohort.RandomSeed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PrepareCandidateBaselinesAsync_RollsBackBaselineArtifactsBeforeLanes()
    {
        var repoPath = CreateRepository();
        var baselineService = new WritingTargetedBaselineService(repoPath, testRunId: 42);
        var store = new InMemoryCandidateBaselineStore();
        var service = MakeService(repoPath, baselineService, store);
        var candidate = new CandidateMethod
        {
            Id = 7,
            MemberId = 11,
            MethodName = "Start"
        };

        var result = await PrepareAsync(service, candidate, repoPath, experimentRunId: 3);

        Assert.Equal(1, baselineService.CallCount);
        Assert.True(result[candidate.Id].Usable);
        Assert.Equal(42, result[candidate.Id].TestRunId);
        Assert.False(Directory.Exists(Path.Combine(repoPath, "coverage")));
        Assert.False(Directory.Exists(Path.Combine(repoPath, "TestMap-Example", "obj")));
        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(repoPath, "tracked.txt")));

        var stored = await store.GetAsync(BaselineKey(candidate));
        Assert.NotNull(stored);
        Assert.Equal(42, stored.TestRunId);
        Assert.True(stored.Usable);
        Assert.Equal(3, stored.ExperimentRunId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PrepareCandidateBaselinesAsync_DoesNotRunWhenNoLaneIsEnabled()
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

        var result = await PrepareAsync(service, candidate, repoPath, experimentRunId: 3, anyLaneEnabled: false);

        Assert.Empty(result);
        Assert.Equal(0, baselineService.CallCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PrepareCandidateBaselinesAsync_OnResume_ReusesStoredBaselineWithoutRemeasuring()
    {
        var repoPath = CreateRepository();
        var baselineService = new WritingTargetedBaselineService(repoPath, testRunId: 99);
        var store = new InMemoryCandidateBaselineStore();
        var candidate = new CandidateMethod { Id = 7, MemberId = 11, MethodName = "Start" };
        await store.SaveAsync(new CandidateBaselineRecord(
            BaselineKey(candidate), ExperimentRunId: 2, CandidateMethodId: 5, MemberId: 11,
            TestRunId: 42, Usable: true, FailureReason: string.Empty));
        var service = MakeService(repoPath, baselineService, store);

        // The resumed run re-inserts the candidate under a new id; the key follows its member.
        var resumed = new CandidateMethod { Id = 8, MemberId = 11, MethodName = "Start" };
        var result = await PrepareAsync(service, resumed, repoPath, experimentRunId: 3);

        Assert.Equal(0, baselineService.CallCount);
        Assert.True(result[resumed.Id].Usable);
        Assert.Equal(42, result[resumed.Id].TestRunId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PrepareCandidateBaselinesAsync_RecordsUnusableBaselineAndDoesNotRemeasureIt()
    {
        var repoPath = CreateRepository();
        var baselineService = new WritingTargetedBaselineService(
            repoPath, testRunId: 42, usable: false, skipReason: "Restore failed.");
        var store = new InMemoryCandidateBaselineStore();
        var service = MakeService(repoPath, baselineService, store);
        var candidate = new CandidateMethod { Id = 7, MemberId = 11, MethodName = "Start" };

        var first = await PrepareAsync(service, candidate, repoPath, experimentRunId: 3);
        var second = await PrepareAsync(service, candidate, repoPath, experimentRunId: 4);

        Assert.Equal(1, baselineService.CallCount);
        Assert.False(first[candidate.Id].Usable);
        Assert.False(second[candidate.Id].Usable);
        Assert.Equal("Restore failed.", second[candidate.Id].SkipReason);
        var stored = await store.GetAsync(BaselineKey(candidate));
        Assert.NotNull(stored);
        Assert.False(stored.Usable);
        Assert.Equal("Restore failed.", stored.FailureReason);
    }

    private const string ResumeGroupId = "series-a";
    private const string RepositoryIdentity = "owner/repo";
    private const string CommitHash = "0123456789abcdef0123456789abcdef01234567";

    private static string BaselineKey(CandidateMethod candidate) =>
        ExperimentResumeService.BuildCandidateBaselineKey(ResumeGroupId, RepositoryIdentity, CommitHash, candidate);

    private static Task<Dictionary<int, TargetedBaselineResult>> PrepareAsync(
        ExperimentOrchestrationService service,
        CandidateMethod candidate,
        string repoPath,
        int experimentRunId,
        bool anyLaneEnabled = true) =>
        service.PrepareCandidateBaselinesAsync(
            experimentRunId,
            ResumeGroupId,
            RepositoryIdentity,
            CommitHash,
            [candidate],
            new Dictionary<int, CandidateMethodContext> { [candidate.MemberId] = MakeContext(candidate, repoPath) },
            anyLaneEnabled,
            CancellationToken.None);

    private static ExperimentOrchestrationService MakeService(
        string repoPath,
        ITargetedBaselineService targetedBaselineService,
        ICandidateBaselineStore? candidateBaselineStore = null)
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
            candidateCohortService: null!,
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
            new RollbackWorkspaceService(context),
            new AlwaysCleanWorkspaceIntegrityService(),
            candidateBaselineStore: candidateBaselineStore);
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
        private readonly bool _usable;
        private readonly string _skipReason;

        public WritingTargetedBaselineService(
            string repoPath,
            int testRunId,
            bool usable = true,
            string skipReason = "")
        {
            _repoPath = repoPath;
            _testRunId = testRunId;
            _usable = usable;
            _skipReason = skipReason;
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

            return new TargetedBaselineResult
            {
                Ran = true,
                TestRunId = _testRunId,
                Usable = _usable,
                SkipReason = _skipReason
            };
        }
    }

    private sealed class AlwaysCleanWorkspaceIntegrityService : IWorkspaceIntegrityService
    {
        public Task<WorkspaceIntegrityObservation> EvaluateAsync(
            IntegrityCheckpoint checkpoint,
            bool allowExpectedChanges = false,
            int? experimentRunId = null,
            string? producerLane = null,
            string? workItemStableKey = null,
            int? attemptNumber = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceIntegrityObservation(
                0, experimentRunId, new string('a', 64), producerLane, workItemStableKey, attemptNumber,
                checkpoint, new string('b', 40), new string('b', 40), true, false,
                WorkspaceIntegrityStatus.VerifiedClean, "clean", DateTimeOffset.UtcNow));
    }
}
