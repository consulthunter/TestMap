using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.AgentTools;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Persistence.Ef.Repositories.AgentTools;
using TestMap.Persistence.Ef.Repositories.RiskScoring;
using TestMap.Rules;
using TestMap.Rules.Generation;
using TestMap.Services.AgentTools;
using TestMap.Services.Experiment.Evaluation;
using TestMap.Services.Experiment.Evaluation.AgentTools;
using TestMap.Services.Configuration;
using TestMap.Services.Rules;
using TestMap.Services.Experiment.Reporting;
using TestMap.Services.Experiment.TaskCards;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration;
using TestMap.Services.TestGeneration.Classification;
using TestMap.Services.TestGeneration.Evidence;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.Strategies;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.Validation;
using TestMap.Services.TestGeneration.Workspace;
using ExperimentTestExecution = TestMap.Models.Experiment.TestExecution;

namespace TestMap.Services.Experiment.Execution;

public class ExperimentOrchestrationService : IExperimentOrchestrationService
{
    private readonly ProjectContext _context;
    private readonly TestMap.Models.Configuration.TestMapConfig _config;
    private readonly IMethodSelectionService _methodSelection;
    private readonly ITestGenerationPipelineService _pipeline;
    private readonly ExperimentRunRepository _experimentRunRepo;
    private readonly ExperimentMatrixWorkItemRepository _workItemRepo;
    private readonly CandidateMethodRepository _candidateMethodRepo;
    private readonly GenerationAttemptRepository _attemptRepo;
    private readonly GenerationStepRepository _stepRepo;
    private readonly TestExecutionRepository _executionRepo;
    private readonly CandidateMethodRiskScoreRepository _riskScoreRepo;
    private readonly IGeneratedTestExecutionService _generatedTestExecutionService;
    private readonly IGenerationValidationService _generationValidationService;
    private readonly IGenerationClassificationService _generationClassificationService;
    private readonly IGenerationExperimentMatrixGenerator _matrixGenerator;
    private readonly IGenerationBudgetExecutor _budgetExecutor;
    private readonly BuildTestService _buildTestService;
    private readonly IExperimentResumeService _resumeService;
    private readonly IRuleDecisionRecorder _ruleDecisionRecorder;
    private readonly IExperimentResultsWriter _resultsWriter;
    private readonly ProjectArtifactCleanupService _artifactCleanupService;
    private readonly TestMapDbContext _dbContext;
    private readonly DockerToolRunner _dockerToolRunner;
    private readonly IAgentToolEnvironmentResolver _agentToolEnvironmentResolver;
    private readonly ToolAttemptRepository _toolAttemptRepo;
    private readonly TaskCardWriter _taskCardWriter;
    private readonly ITargetedBaselineService _targetedBaselineService;
    private readonly IToolPostAttemptAnalysisService _toolPostAttemptAnalysisService;
    private readonly IToolAttemptGeneratedTestService _toolAttemptGeneratedTestService;
    private readonly IToolPostAttemptMeasurementService _toolPostAttemptMeasurementService;

    private readonly
        IReadOnlyDictionary<TestMap.Models.Configuration.Testing.Generation.TestGenerationApproach,
            ITestGenerationApproach> _generationApproaches;

    private readonly RollbackWorkspaceService _workspace;
    private ExperimentConfig? _activeExperimentConfig;
    private int? _activeExperimentRunId;

    public ExperimentOrchestrationService(
        ProjectContext context,
        TestMap.Models.Configuration.TestMapConfig config,
        IMethodSelectionService methodSelection,
        ITestGenerationPipelineService pipeline,
        ExperimentRunRepository experimentRunRepo,
        ExperimentMatrixWorkItemRepository workItemRepo,
        CandidateMethodRepository candidateMethodRepo,
        GenerationAttemptRepository attemptRepo,
        GenerationStepRepository stepRepo,
        TestExecutionRepository executionRepo,
        CandidateMethodRiskScoreRepository riskScoreRepo,
        IGeneratedTestExecutionService generatedTestExecutionService,
        IGenerationValidationService generationValidationService,
        IGenerationClassificationService generationClassificationService,
        IGenerationExperimentMatrixGenerator matrixGenerator,
        IGenerationBudgetExecutor budgetExecutor,
        BuildTestService buildTestService,
        IExperimentResumeService resumeService,
        IRuleDecisionRecorder ruleDecisionRecorder,
        IExperimentResultsWriter resultsWriter,
        ProjectArtifactCleanupService artifactCleanupService,
        TestMapDbContext dbContext,
        DockerToolRunner dockerToolRunner,
        IAgentToolEnvironmentResolver agentToolEnvironmentResolver,
        ToolAttemptRepository toolAttemptRepo,
        TaskCardWriter taskCardWriter,
        ITargetedBaselineService targetedBaselineService,
        IToolPostAttemptAnalysisService toolPostAttemptAnalysisService,
        IToolAttemptGeneratedTestService toolAttemptGeneratedTestService,
        IToolPostAttemptMeasurementService toolPostAttemptMeasurementService,
        IEnumerable<ITestGenerationApproach> generationApproaches,
        RollbackWorkspaceService workspace)
    {
        _context = context;
        _config = config;
        _methodSelection = methodSelection;
        _pipeline = pipeline;
        _experimentRunRepo = experimentRunRepo;
        _workItemRepo = workItemRepo;
        _candidateMethodRepo = candidateMethodRepo;
        _attemptRepo = attemptRepo;
        _stepRepo = stepRepo;
        _executionRepo = executionRepo;
        _riskScoreRepo = riskScoreRepo;
        _generatedTestExecutionService = generatedTestExecutionService;
        _generationValidationService = generationValidationService;
        _generationClassificationService = generationClassificationService;
        _matrixGenerator = matrixGenerator;
        _budgetExecutor = budgetExecutor;
        _buildTestService = buildTestService;
        _resumeService = resumeService;
        _ruleDecisionRecorder = ruleDecisionRecorder;
        _resultsWriter = resultsWriter;
        _artifactCleanupService = artifactCleanupService;
        _dbContext = dbContext;
        _dockerToolRunner = dockerToolRunner;
        _agentToolEnvironmentResolver = agentToolEnvironmentResolver;
        _toolAttemptRepo = toolAttemptRepo;
        _taskCardWriter = taskCardWriter;
        _targetedBaselineService = targetedBaselineService;
        _toolPostAttemptAnalysisService = toolPostAttemptAnalysisService;
        _toolAttemptGeneratedTestService = toolAttemptGeneratedTestService;
        _toolPostAttemptMeasurementService = toolPostAttemptMeasurementService;
        _generationApproaches = generationApproaches.ToDictionary(x => x.Strategy);
        _workspace = workspace;
    }

    public async Task<ExperimentRun> RunExperimentAsync(
        ExperimentConfig config,
        CancellationToken cancellationToken = default)
    {
        _activeExperimentConfig = config;
        await _workspace.EnsureWorkspaceReadyAsync(cancellationToken);
        _artifactCleanupService.CleanupProjectDirectory(false);
        var experimentStopwatch = Stopwatch.StartNew();

        _context.Project.Logger?.Information("=== Starting Experiment Run ===");
        _context.Project.Logger?.Information($"Providers: {string.Join(", ", config.IncludeProviders)}");
        _context.Project.Logger?.Information($"Budget Modes: {string.Join(", ", config.BudgetModes)}");
        _context.Project.Logger?.Information($"Approaches: {string.Join(", ", config.Approaches)}");
        _context.Project.Logger?.Information(
            $"Candidate selection override: {config.CandidateSelectionStrategy?.ToString() ?? "<global>"}");
        _context.Project.Logger?.Information($"Candidate limit: {config.CandidateLimit}");

        var experimentRun = new ExperimentRun
        {
            Name = $"Experiment_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            ConfigurationJson = JsonSerializer.Serialize(config),
            StartedAt = DateTime.UtcNow,
            ProjectId = _context.Project.DbId,
            Objective = config.Objective.ToString(),
            CandidateSelectionStrategy = config.CandidateSelectionStrategy?.ToString()
                                         ?? string.Empty,
            CandidateLimit = config.CandidateLimit,
            ResultsFilePath = ResolveResultsFilePath(config),
            Status = "Running"
        };

        experimentRun.Id = await _experimentRunRepo.InsertAsync(experimentRun, cancellationToken);
        _activeExperimentRunId = experimentRun.Id;

        try
        {
            var candidateMethods = await _methodSelection.SelectCandidateMethodsAsync(
                config,
                requirePassingExistingTest: ShouldRequirePassingExistingTest(config.Objective),
                cancellationToken);
            _context.Project.Logger?.Information($"Selected {candidateMethods.Count} candidate methods");

            foreach (var method in candidateMethods)
            {
                method.ExperimentRunId = experimentRun.Id;
                method.Id = await _candidateMethodRepo.InsertAsync(method, cancellationToken);
                await SaveRiskScoreAsync(method, cancellationToken);
            }

            var matrix = new GenerationExperimentMatrix();
            if (config.Evaluation.TestMap.Enabled)
            {
                var providers = GetProvidersToTest(config);
                matrix = _matrixGenerator.Generate(config, providers);
                await _ruleDecisionRecorder.RecordAsync(
                    _context.Project.DbId,
                    RuleDecisionScope.ExperimentRun(experimentRun.Id),
                    matrix.RuleDecisions,
                    experimentRunId: experimentRun.Id,
                    cancellationToken: cancellationToken);
                _context.Project.Logger?.Information("Expanded {MatrixCount} experiment matrix item(s).", matrix.Items.Count);
            }

            var configuredTools = ResolveConfiguredTools(config);
            var toolAvailabilityPlan = new AgentToolAvailabilityPlan();
            if (config.Evaluation.Tools.Enabled)
                toolAvailabilityPlan = await EnsureToolAvailabilityAsync(config, configuredTools, cancellationToken);

            var methodContextsByMemberId = await ResolveCandidateContextsAsync(
                candidateMethods,
                config,
                cancellationToken);
            await EnsureExperimentMutationBaselinesAsync(
                experimentRun.Id,
                matrix,
                methodContextsByMemberId.Values,
                cancellationToken);
            var targetedBaselinesByCandidateId = await PrecomputeTargetedBaselinesAsync(
                experimentRun.Id,
                candidateMethods,
                methodContextsByMemberId,
                toolAvailabilityPlan,
                cancellationToken);

            foreach (var candidateMethod in candidateMethods)
            {
                _context.Project.Logger?.Information($"\n--- Method: {candidateMethod.MethodName} ---");

                if (!methodContextsByMemberId.TryGetValue(candidateMethod.MemberId, out var methodContext))
                {
                    _context.Project.Logger?.Warning($"Could not get context for method {candidateMethod.MethodName}");
                    continue;
                }

                var matrixApproaches = matrix.Items.Select(x => x.Approach).Distinct().ToList();
                if (config.Evaluation.TestMap.Enabled &&
                    matrixApproaches.Count > 0 &&
                    matrixApproaches.All(x => ResolveGenerationApproach(x).ShouldSkipGeneration(methodContext)))
                {
                    _context.Project.Logger?.Information(
                        "Skipping method {MethodName} because every matrix generation approach marked it as skip.",
                        candidateMethod.MethodName);
                    continue;
                }

                candidateMethod.ExistingTestMemberId = methodContext.Method.ExistingTestMemberId;
                candidateMethod.ExistingTestMethodName = methodContext.Method.ExistingTestMethodName;
                await _candidateMethodRepo.UpdateAsync(candidateMethod, cancellationToken);

                if (config.Evaluation.TestMap.Enabled)
                    foreach (var matrixItem in matrix.Items)
                        await ExecuteTestMapMatrixItemAsync(
                            experimentRun,
                            candidateMethod,
                            methodContext,
                            matrixItem,
                            cancellationToken);

                if (config.Evaluation.Tools.Enabled)
                {
                    targetedBaselinesByCandidateId.TryGetValue(
                        candidateMethod.Id,
                        out var targetedBaseline);
                    await ExecuteToolEvaluationAsync(
                        experimentRun,
                        candidateMethod,
                        methodContext,
                        toolAvailabilityPlan,
                        targetedBaseline ?? new TargetedBaselineResult
                        {
                            Ran = false,
                            SkipReason = "No precomputed targeted baseline was available for this candidate."
                        },
                        cancellationToken);
                }
            }

            experimentStopwatch.Stop();
            experimentRun.CompletedAt = DateTime.UtcNow;
            experimentRun.Status = "Completed";
            await _experimentRunRepo.UpdateAsync(experimentRun, cancellationToken);

            _context.Project.Logger?.Information(
                $"\n=== Experiment Complete in {experimentStopwatch.Elapsed.TotalSeconds:F2}s ===");

            return experimentRun;
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error(ex, "Experiment failed.");
            experimentStopwatch.Stop();
            experimentRun.CompletedAt = DateTime.UtcNow;
            experimentRun.Status = "Failed";
            await _experimentRunRepo.UpdateAsync(experimentRun, cancellationToken);
            throw;
        }
    }

    private async Task<Dictionary<int, CandidateMethodContext>> ResolveCandidateContextsAsync(
        IReadOnlyCollection<CandidateMethod> candidateMethods,
        ExperimentConfig config,
        CancellationToken cancellationToken)
    {
        var contexts = new Dictionary<int, CandidateMethodContext>();
        var contextMappingMode = config.ContextMappingMode ??
                                 _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection.ContextMappingMode;

        foreach (var candidateMethod in candidateMethods)
        {
            var context = await _methodSelection.GetMethodContextAsync(
                candidateMethod.MemberId,
                contextMappingMode,
                cancellationToken);

            if (context != null)
                contexts[candidateMethod.MemberId] = context;
        }

        return contexts;
    }

    private async Task EnsureExperimentMutationBaselinesAsync(
        int experimentRunId,
        GenerationExperimentMatrix matrix,
        IEnumerable<CandidateMethodContext> methodContexts,
        CancellationToken cancellationToken)
    {
        if (!matrix.Items.Any(UsesMutationMetrics))
            return;

        var groups = methodContexts
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.SourceProjectPath) &&
                !string.IsNullOrWhiteSpace(x.TestProjectPath))
            .GroupBy(x => new MutationBaselineKey(
                NormalizeProjectPath(x.SourceProjectPath),
                NormalizeProjectPath(x.TestProjectPath),
                x.TargetBuildFramework?.Trim() ?? string.Empty))
            .ToList();

        if (groups.Count == 0)
            return;

        _context.Project.Logger?.Information(
            "Running {Count} targeted mutation baseline(s) for experiment-scoped mutation comparisons.",
            groups.Count);

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = group.Key;
            _context.Project.Logger?.Information(
                "Running targeted mutation baseline: SourceProject={SourceProject}, TestProject={TestProject}, TargetFramework={TargetFramework}",
                key.SourceProjectPath,
                key.TestProjectPath,
                string.IsNullOrWhiteSpace(key.TargetFramework) ? "<default>" : key.TargetFramework);

            await _buildTestService.BuildTestAsync(
                BuildTestRunRequest.CreateIteration(
                    key.TestProjectPath,
                    key.TargetFramework,
                    coveredMethodName: null,
                    key.SourceProjectPath,
                    experimentRunId,
                    isMutationBaseline: true));
        }
    }

    private static bool UsesMutationMetrics(GenerationExperimentMatrixItem item)
    {
        return item.MetricsPath is MetricsDrivenPath.Mutation or MetricsDrivenPath.CoverageAndMutation;
    }

    private static string NormalizeProjectPath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed record MutationBaselineKey(
        string SourceProjectPath,
        string TestProjectPath,
        string TargetFramework);

    internal async Task<Dictionary<int, TargetedBaselineResult>> PrecomputeTargetedBaselinesAsync(
        int experimentRunId,
        IReadOnlyCollection<CandidateMethod> candidateMethods,
        IReadOnlyDictionary<int, CandidateMethodContext> methodContextsByMemberId,
        AgentToolAvailabilityPlan toolAvailabilityPlan,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<int, TargetedBaselineResult>();
        if (toolAvailabilityPlan.ExecutableTools.Count == 0)
            return results;

        foreach (var candidateMethod in candidateMethods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!methodContextsByMemberId.TryGetValue(candidateMethod.MemberId, out var methodContext))
                continue;

            try
            {
                var result = await _targetedBaselineService.RunAsync(
                    experimentRunId,
                    candidateMethod,
                    methodContext,
                    cancellationToken);
                results[candidateMethod.Id] = result;

                if (!result.Ran)
                    _context.Project.Logger?.Warning(
                        "Skipping precomputed targeted baseline for {MethodName}: {Reason}",
                        candidateMethod.MethodName,
                        result.SkipReason);
            }
            finally
            {
                // Baseline collection writes coverage, mutation, bin, and obj artifacts.
                // Clear them before either evaluation lane starts so the first lane/tool
                // sees the same clean workspace as later attempts.
                await _workspace.RollbackChangesAsync(cancellationToken);
            }
        }

        return results;
    }

    private async Task<ExperimentMatrixWorkItem> EnsureWorkItemAsync(
        ExperimentRun experimentRun,
        CandidateMethod candidateMethod,
        GenerationExperimentMatrixItem matrixItem,
        CancellationToken cancellationToken)
    {
        var resumeGroupId = string.IsNullOrWhiteSpace(_activeExperimentConfig?.Resume.ResumeRunId)
            ? experimentRun.Id.ToString()
            : _activeExperimentConfig!.Resume.ResumeRunId!;
        var repositoryIdentity = $"{_context.Project.Owner}/{_context.Project.RepoName}";
        var commitHash = _context.Project.Commit ?? _context.Project.LastAnalyzedCommit ?? _context.CurrentCommit ?? string.Empty;
        var candidateWorkItem = _resumeService.CreateWorkItem(
            experimentRun.Id,
            resumeGroupId,
            repositoryIdentity,
            commitHash,
            GetActiveObjective(),
            candidateMethod,
            matrixItem);
        var existing = await _workItemRepo.GetByStableKeyAsync(candidateWorkItem.StableKey, cancellationToken);
        if (existing != null) return existing;

        candidateWorkItem.Id = await _workItemRepo.UpsertAsync(candidateWorkItem, cancellationToken);
        return candidateWorkItem;
    }

    private async Task ExecuteTestMapMatrixItemAsync(
        ExperimentRun experimentRun,
        CandidateMethod candidateMethod,
        CandidateMethodContext methodContext,
        GenerationExperimentMatrixItem matrixItem,
        CancellationToken cancellationToken)
    {
        var workItem = await EnsureWorkItemAsync(
            experimentRun,
            candidateMethod,
            matrixItem,
            cancellationToken);
        var resumeDecision = _resumeService.Evaluate(workItem, _activeExperimentConfig!.Resume, DateTime.UtcNow);
        workItem = resumeDecision.WorkItem;
        await _ruleDecisionRecorder.RecordAsync(
            _context.Project.DbId,
            RuleDecisionScope.ExperimentMatrixWorkItem(workItem.Id),
            resumeDecision.RuleDecisions,
            experimentRunId: experimentRun.Id,
            candidateMethodId: candidateMethod.Id,
            cancellationToken: cancellationToken);

        if (!resumeDecision.ShouldExecute)
        {
            await _workItemRepo.UpsertAsync(workItem, cancellationToken);
            return;
        }

        _context.Project.Logger?.Information("  Variant: {VariantId}", matrixItem.VariantId);

        try
        {
            await _workItemRepo.UpdateStatusAsync(
                workItem.Id,
                ExperimentMatrixWorkItemStatus.Running,
                cancellationToken: cancellationToken);

            var attempts = await ExecuteGenerationAttemptAsync(
                candidateMethod.Id,
                methodContext,
                matrixItem,
                cancellationToken);

            var persistedAttemptIdsByAttemptNumber = new Dictionary<int, int>();
            // Track running cumulative for repair chains.  Resets at each independent
            // generation attempt (PassAt5) so that PassAt1RepairAt5 accumulates across
            // the whole chain while PassAt5 reports per-attempt costs.
            var chainCumulativeTokens = 0;
            foreach (var attempt in attempts)
            {
                if (attempt.ParentAttemptNumber.HasValue &&
                    persistedAttemptIdsByAttemptNumber.TryGetValue(
                        attempt.ParentAttemptNumber.Value,
                        out var parentAttemptId))
                    attempt.ParentAttemptId = parentAttemptId;
                attempt.ExperimentMatrixWorkItemId = workItem.Id;

                // Cumulative resets at each fresh (non-repair) generation attempt.
                if (!attempt.IsRepairAttempt)
                    chainCumulativeTokens = 0;
                chainCumulativeTokens += attempt.TotalTokensUsed;
                attempt.ChainCumulativeTokensUsed = chainCumulativeTokens;

                var persistedAttemptId = await SaveGenerationAttemptAsync(
                    experimentRun.Id,
                    candidateMethod.Id,
                    attempt,
                    cancellationToken);
                persistedAttemptIdsByAttemptNumber[attempt.AttemptNumber] = persistedAttemptId;
                await _resultsWriter.AppendAsync(
                    experimentRun,
                    await CreateResultFileRowAsync(
                        experimentRun,
                        candidateMethod,
                        attempt,
                        workItem.StableKey,
                        cancellationToken),
                    cancellationToken);
            }

            await _workItemRepo.UpdateStatusAsync(
                workItem.Id,
                ExperimentMatrixWorkItemStatus.Completed,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            await _workItemRepo.UpdateStatusAsync(
                workItem.Id,
                ExperimentMatrixWorkItemStatus.Failed,
                ex.Message,
                cancellationToken);
            _context.Project.Logger?.Error(
                ex,
                "Failed to execute {VariantId} for {MethodName}",
                matrixItem.VariantId,
                candidateMethod.MethodName);
        }
    }

    private IReadOnlyList<ExperimentToolConfig> ResolveConfiguredTools(ExperimentConfig config)
    {
        if (!config.Evaluation.Tools.Enabled)
            return [];

        var tools = config.Tools.Count > 0
            ? config.Tools
            : config.Evaluation.Tools.ToolIds
                .Select(id => new ExperimentToolConfig { Id = id, ImageKey = id })
                .ToList();

        if (config.Evaluation.Tools.ToolIds.Count > 0)
        {
            var included = config.Evaluation.Tools.ToolIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            tools = tools.Where(x => included.Contains(x.Id)).ToList();
        }

        if (tools.Count == 0)
            throw new InvalidOperationException("Tool evaluation is enabled but no ExperimentConfig.Tools or Evaluation.Tools.ToolIds were configured.");

        return tools;
    }

    internal static IReadOnlyList<GenerationBudgetMode> ResolveAgentToolBudgetModes(ExperimentConfig config)
    {
        var modes = config.BudgetModes.Count == 0
            ? [GenerationBudgetMode.PassAt1]
            : config.BudgetModes;

        var resolved = modes
            .Where(x => x is GenerationBudgetMode.PassAt1 or GenerationBudgetMode.PassAt5)
            .Distinct()
            .ToList();

        return resolved.Count == 0
            ? [GenerationBudgetMode.PassAt1]
            : resolved;
    }

    internal static int GetAgentToolAttemptCount(GenerationBudgetMode budgetMode) =>
        budgetMode == GenerationBudgetMode.PassAt5 ? 5 : 1;

    private async Task<AgentToolAvailabilityPlan> EnsureToolAvailabilityAsync(
        ExperimentConfig config,
        IReadOnlyList<ExperimentToolConfig> tools,
        CancellationToken cancellationToken)
    {
        var results = new List<ToolAvailabilityResult>();
        foreach (var tool in tools)
        {
            var availability = await _dockerToolRunner.CheckAvailabilityAsync(tool, cancellationToken);
            results.Add(availability);
            if (availability.IsAvailable)
            {
                _context.Project.Logger?.Information(
                    "Agent tool '{ToolId}' is available via image '{ImageName}'.",
                    tool.Id,
                    availability.ImageName);
            }
        }

        var plan = AgentToolAvailabilityPlan.Create(
            tools,
            results,
            config.Evaluation.Tools.RequireAvailabilityInSetup);
        if (plan.SetupFailures.Count > 0)
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                plan.SetupFailures.Select(x => $"Agent tool '{x.Tool.Id}' is unavailable. {x.Reason}")));

        foreach (var decision in plan.SkippedTools)
            _context.Project.Logger?.Warning(
                "Skipping unavailable agent tool '{ToolId}': {Reason}",
                decision.Tool.Id,
                decision.Reason);

        return plan;
    }

    private async Task ExecuteToolEvaluationAsync(
        ExperimentRun experimentRun,
        CandidateMethod candidateMethod,
        CandidateMethodContext methodContext,
        AgentToolAvailabilityPlan availabilityPlan,
        TargetedBaselineResult targetedBaseline,
        CancellationToken cancellationToken)
    {
        var tools = availabilityPlan.ExecutableTools;
        var budgetModes = ResolveAgentToolBudgetModes(_activeExperimentConfig!);
        var ignoredBudgetModes = _activeExperimentConfig!.BudgetModes
            .Where(x => x is not GenerationBudgetMode.PassAt1 and not GenerationBudgetMode.PassAt5)
            .Distinct()
            .ToList();
        foreach (var ignored in ignoredBudgetModes)
            _context.Project.Logger?.Warning(
                "Skipping unsupported agent-tool budget mode '{BudgetMode}'. Agent tools run only pass@1 and pass@5.",
                ignored);

        if (!targetedBaseline.Ran)
            _context.Project.Logger?.Warning(
                "Skipping targeted baseline for {MethodName}: {Reason}",
                candidateMethod.MethodName,
                targetedBaseline.SkipReason);

        var lane = new AgentToolEvaluationLane(
            _dockerToolRunner,
            _agentToolEnvironmentResolver,
            _toolAttemptRepo,
            tools,
            _context,
            _config,
            _taskCardWriter);

        foreach (var skipped in availabilityPlan.SkippedTools)
        {
            foreach (var budgetMode in budgetModes)
                await RecordSkippedToolAttemptAsync(
                    experimentRun,
                    candidateMethod,
                    methodContext,
                    skipped,
                    budgetMode,
                    targetedBaseline.TestRunId,
                    cancellationToken);
        }

        foreach (var tool in tools)
        {
            foreach (var budgetMode in budgetModes)
            {
                var workItem = await EnsureToolWorkItemAsync(
                    experimentRun,
                    candidateMethod,
                    methodContext,
                    tool,
                    budgetMode,
                    cancellationToken);

                var resumeDecision = _resumeService.Evaluate(workItem, _activeExperimentConfig!.Resume, DateTime.UtcNow);
                workItem = resumeDecision.WorkItem;

                if (!resumeDecision.ShouldExecute)
                {
                    await _workItemRepo.UpsertAsync(workItem, cancellationToken);
                    continue;
                }

                await _workItemRepo.UpdateStatusAsync(
                    workItem.Id,
                    ExperimentMatrixWorkItemStatus.Running,
                    cancellationToken: cancellationToken);

                var anySucceeded = false;
                string? lastError = null;
                for (var attemptNumber = 1; attemptNumber <= GetAgentToolAttemptCount(budgetMode); attemptNumber++)
                {
                    // Execute and refresh inside try/finally so the workspace is always reset to HEAD
                    // before the next tool attempt runs, even on crash or timeout.
                    ExperimentEvaluationAttemptResult? result = null;
                    Stopwatch? validationStopwatch = null;
                    try
                    {
                        result = await lane.ExecuteAsync(
                            new ExperimentEvaluationWorkItemContext
                            {
                                WorkItem = workItem,
                                Candidate = candidateMethod,
                                TargetedBaselineId = targetedBaseline.TestRunId,
                                MethodContext = methodContext
                            },
                            cancellationToken);

                        anySucceeded |= result.Success;
                        lastError = result.ErrorMessage ?? lastError;

                        if (result.ToolAttempt is { RunStatus: ToolRunStatus.Completed, ChangedFilesCount: > 0 })
                        {
                            validationStopwatch = Stopwatch.StartNew();

                            // Re-analyze the workspace: update the code graph, collect code metrics,
                            // and collect test smells for the test project. This must run before
                            // LinkAsync so that newly-generated test member IDs exist in the DB.
                            var analysis = await _toolPostAttemptAnalysisService.AnalyzeAsync(
                                methodContext,
                                cancellationToken);
                            if (!analysis.Analyzed)
                                _context.Project.Logger?.Warning(
                                    "Skipping post-attempt analysis for tool attempt {ToolAttemptId}: {Reason}",
                                    result.ToolAttempt.Id,
                                    analysis.SkipReason);

                            // Link test members in changed files to the tool attempt.
                            var linkResult = await _toolAttemptGeneratedTestService.LinkAsync(
                                result.ToolAttempt,
                                result.ChangedFiles,
                                _context.Project.DbId,
                                cancellationToken);
                            if (linkResult.LinkedCount > 0)
                                _context.Project.Logger?.Information(
                                    "Linked {Count} test member(s) to tool attempt {ToolAttemptId}.",
                                    linkResult.LinkedCount,
                                    result.ToolAttempt.Id);

                            // Run build/test measurement on the modified workspace and reclassify outcome.
                            var measurement = await _toolPostAttemptMeasurementService.MeasureAsync(
                                result.ToolAttempt,
                                candidateMethod,
                                methodContext,
                                cancellationToken);
                            if (!measurement.Measured)
                                _context.Project.Logger?.Warning(
                                    "Skipping post-attempt measurement for tool attempt {ToolAttemptId}: {Reason}",
                                    result.ToolAttempt.Id,
                                    measurement.SkipReason);
                        }
                    }
                    finally
                    {
                        if (result?.ToolAttempt != null)
                        {
                            validationStopwatch?.Stop();
                            result.ToolAttempt.GenerationDurationSeconds = result.ToolAttempt.ElapsedSeconds;
                            result.ToolAttempt.ValidationDurationSeconds =
                                validationStopwatch?.Elapsed.TotalSeconds ?? 0;
                            result.ToolAttempt.TotalAttemptDurationSeconds =
                                result.ToolAttempt.GenerationDurationSeconds
                                + result.ToolAttempt.ValidationDurationSeconds;
                            result.ToolAttempt.CompletedAt = DateTime.UtcNow;
                            await _toolAttemptRepo.UpdateAsync(result.ToolAttempt, cancellationToken);
                        }

                        // Always restore the workspace to HEAD so the next tool attempt starts clean.
                        await _workspace.RollbackChangesAsync(cancellationToken);
                    }

                    if (result?.ToolAttempt != null)
                    {
                        var toolRows = await CreateToolResultFileRowsAsync(
                            experimentRun,
                            candidateMethod,
                            methodContext,
                            workItem,
                            result.ToolAttempt,
                            attemptNumber,
                            cancellationToken);
                        foreach (var toolRow in toolRows)
                            await _resultsWriter.AppendAsync(experimentRun, toolRow, cancellationToken);
                    }
                }

                await _workItemRepo.UpdateStatusAsync(
                    workItem.Id,
                    anySucceeded
                        ? ExperimentMatrixWorkItemStatus.Completed
                        : ExperimentMatrixWorkItemStatus.Failed,
                    lastError,
                    cancellationToken);
            }
        }
    }

    private async Task RecordSkippedToolAttemptAsync(
        ExperimentRun experimentRun,
        CandidateMethod candidateMethod,
        CandidateMethodContext methodContext,
        AgentToolAvailabilityDecision decision,
        GenerationBudgetMode budgetMode,
        int? targetedBaselineId,
        CancellationToken cancellationToken)
    {
        var workItem = await EnsureToolWorkItemAsync(
            experimentRun,
            candidateMethod,
            methodContext,
            decision.Tool,
            budgetMode,
            cancellationToken);

        await _workItemRepo.UpdateStatusAsync(
            workItem.Id,
            ExperimentMatrixWorkItemStatus.Skipped,
            decision.Reason,
            cancellationToken);

        var attempt = new ToolAttempt
        {
            ExperimentRunId = experimentRun.Id,
            MatrixWorkItemId = workItem.Id,
            CandidateMethodId = candidateMethod.Id,
            TargetedBaselineId = targetedBaselineId,
            ToolId = decision.Tool.Id,
            ImageName = decision.Availability.ImageName ?? string.Empty,
            ImageKey = decision.Tool.ImageKey ?? decision.Tool.Id,
            RunStatus = ToolRunStatus.Skipped,
            ValidationOutcome = ToolValidationOutcome.Skipped,
            ObservedOutcome = ToolObservedOutcome.Skipped,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            TimeoutSeconds = decision.Tool.TimeoutMinutes * 60,
            Model = workItem.ModelName,
            ProviderId = workItem.Provider.ToString(),
            Notes = decision.Reason
        };
        attempt.Id = await _toolAttemptRepo.InsertAsync(attempt, cancellationToken);

        var skippedRows = await CreateToolResultFileRowsAsync(
            experimentRun,
            candidateMethod,
            methodContext,
            workItem,
            attempt,
            1,
            cancellationToken);
        foreach (var skippedRow in skippedRows)
            await _resultsWriter.AppendAsync(experimentRun, skippedRow, cancellationToken);
    }

    private async Task<ExperimentMatrixWorkItem> EnsureToolWorkItemAsync(
        ExperimentRun experimentRun,
        CandidateMethod candidateMethod,
        CandidateMethodContext methodContext,
        ExperimentToolConfig tool,
        GenerationBudgetMode budgetMode,
        CancellationToken cancellationToken)
    {
        var resumeGroupId = string.IsNullOrWhiteSpace(_activeExperimentConfig?.Resume.ResumeRunId)
            ? experimentRun.Id.ToString()
            : _activeExperimentConfig!.Resume.ResumeRunId!;
        var repositoryIdentity = $"{_context.Project.Owner}/{_context.Project.RepoName}";
        var commitHash = _context.Project.Commit ?? _context.Project.LastAnalyzedCommit ?? _context.CurrentCommit ?? string.Empty;
        var provider = tool.Provider ?? _config.TestingConfig.GenerationConfig.Provider;
        var modelName = tool.Model ?? _config.AiProviderConfig.GetProviderConfig(provider)?.Model ?? string.Empty;
        var stableKey = string.Join(
            "|",
            "tool",
            resumeGroupId,
            repositoryIdentity,
            commitHash,
            experimentRun.Objective,
            candidateMethod.MemberId,
            budgetMode,
            tool.Id,
            modelName);

        var candidateWorkItem = new ExperimentMatrixWorkItem
        {
            ExperimentRunId = experimentRun.Id,
            CandidateMethodId = candidateMethod.Id,
            MemberId = candidateMethod.MemberId,
            StableKey = stableKey,
            Status = ExperimentMatrixWorkItemStatus.Pending,
            Provider = provider,
            ModelName = modelName,
            Objective = _activeExperimentConfig?.Objective
                        ?? TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective.TestSuiteExpansion,
            Approach = TestMap.Models.Configuration.Testing.Generation.TestGenerationApproach.MetricsDriven,
            MetricsPath = _activeExperimentConfig?.MetricsPaths.FirstOrDefault(),
            ContextMode = _activeExperimentConfig?.ContextModes.FirstOrDefault()
                          ?? _config.TestingConfig.GenerationConfig.ContextMode,
            BudgetMode = budgetMode,
            AblationVariantId = tool.Id,
            StepConfigJson = "{}",
            CreatedAt = DateTime.UtcNow
        };

        var existing = await _workItemRepo.GetByStableKeyAsync(candidateWorkItem.StableKey, cancellationToken);
        if (existing != null) return existing;

        candidateWorkItem.Id = await _workItemRepo.UpsertAsync(candidateWorkItem, cancellationToken);
        return candidateWorkItem;
    }

    private async Task<ExperimentResultFileRow> CreateResultFileRowAsync(
        ExperimentRun experimentRun,
        CandidateMethod candidateMethod,
        GenerationAttempt attempt,
        string stableKey,
        CancellationToken cancellationToken)
    {
        var execution = attempt.TestExecution;
        var generatedTestMemberId = await ResolveLatestTestMemberIdAsync(
            execution?.GeneratedTestMethodName,
            cancellationToken);
        var generatedTestCompiled = execution?.CompilationSuccess ?? false;
        var generatedTestExecuted = generatedTestCompiled && (execution?.TestsExecuted ?? false);
        var generatedTestPassed = generatedTestCompiled && generatedTestExecuted && (execution?.TestPassed ?? false);
        var sourceMetrics = await GetMemberCodeMetricsAsync(candidateMethod.MemberId, cancellationToken);
        var baselineMetrics = await GetMemberCodeMetricsAsync(candidateMethod.ExistingTestMemberId, cancellationToken);
        var generatedMetrics = await GetMemberCodeMetricsAsync(generatedTestMemberId, cancellationToken);
        var baselineTestExecutionTimeMs = await GetBaselineTestExecutionTimeMsAsync(
            candidateMethod.ExistingTestMethodName,
            candidateMethod.ExistingTestMemberId,
            cancellationToken);
        var accessReport = ResolveAccessPathReport(attempt.RuleDecisionSnapshotJson);

        return new ExperimentResultFileRow
        {
            ExperimentRunId = experimentRun.Id,
            RepoUrl = _context.Project.GitHubUrl,
            RepoOwner = _context.Project.Owner,
            RepoName = _context.Project.RepoName,
            CommitHash = _context.Project.Commit ?? _context.Project.LastAnalyzedCommit ?? _context.CurrentCommit ?? string.Empty,
            RunDate = DateTime.UtcNow,
            Objective = experimentRun.Objective,
            TargetSelectionStrategy = experimentRun.CandidateSelectionStrategy,
            GenerationApproach = attempt.GenerationApproach,
            MetricsPath = attempt.MetricsPath,
            SourceMethodMaintainabilityIndex = sourceMetrics?.MaintainabilityIndex,
            SourceMethodCyclomaticComplexity = sourceMetrics?.CyclomaticComplexity,
            SourceMethodClassCoupling = sourceMetrics?.ClassCoupling,
            SourceMethodDepthOfInheritance = sourceMetrics?.DepthOfInheritance,
            SourceMethodSourceLinesOfCode = sourceMetrics?.SourceLinesOfCode,
            SourceMethodExecutableLinesOfCode = sourceMetrics?.ExecutableLinesOfCode,
            BaselineTestMaintainabilityIndex = baselineMetrics?.MaintainabilityIndex,
            BaselineTestCyclomaticComplexity = baselineMetrics?.CyclomaticComplexity,
            BaselineTestClassCoupling = baselineMetrics?.ClassCoupling,
            BaselineTestDepthOfInheritance = baselineMetrics?.DepthOfInheritance,
            BaselineTestSourceLinesOfCode = baselineMetrics?.SourceLinesOfCode,
            BaselineTestExecutableLinesOfCode = baselineMetrics?.ExecutableLinesOfCode,
            GeneratedTestMaintainabilityIndex = generatedMetrics?.MaintainabilityIndex,
            GeneratedTestCyclomaticComplexity = generatedMetrics?.CyclomaticComplexity,
            GeneratedTestClassCoupling = generatedMetrics?.ClassCoupling,
            GeneratedTestDepthOfInheritance = generatedMetrics?.DepthOfInheritance,
            GeneratedTestSourceLinesOfCode = generatedMetrics?.SourceLinesOfCode,
            GeneratedTestExecutableLinesOfCode = generatedMetrics?.ExecutableLinesOfCode,
            BaselineTestSmells = await GetTestSmellSummaryAsync(
                candidateMethod.ExistingTestMethodName,
                candidateMethod.ExistingTestMemberId,
                cancellationToken),
            GeneratedTestSmells = await GetTestSmellSummaryAsync(
                execution?.GeneratedTestMethodName,
                generatedTestMemberId,
                cancellationToken),
            Provider = attempt.Provider,
            Model = attempt.ModelName ?? string.Empty,
            ContextMode = attempt.ContextMode,
            BudgetMode = attempt.BudgetMode,
            AblationVariantId = attempt.AblationVariantId,
            StepsIncluded = attempt.StepConfigJson,
            SourceMemberVisibility = accessReport.Visibility,
            AccessStrategy = accessReport.AccessStrategy,
            AccessPathMemberIds = accessReport.PathMemberIds,
            TestMappingCount = accessReport.MappingCount,
            SetupBindingCount = accessReport.SetupBindingCount,
            AttemptNumber = attempt.AttemptNumber,
            RepairAttemptNumber = attempt.IsRepairAttempt ? attempt.AttemptNumber : null,
            SourceMemberId = candidateMethod.MemberId,
            SourceMethodName = candidateMethod.MethodName,
            SourceMethodSignature = candidateMethod.Signature,
            CandidateTestIntentionsSummary = candidateMethod.TestIntentionsSummary,
            CandidateTypeConstructionSummary = candidateMethod.TypeConstructionSummary,
            CandidateMetadataJson = candidateMethod.CandidateMetadataJson,
            SourceMethodBaselineCoverage = candidateMethod.BaselineCoverage,
            SourceMethodComplexity = candidateMethod.ComplexityScore,
            BaselineTestState = candidateMethod.TestState.ToString(),
            BaselineTestMethod = candidateMethod.ExistingTestMethodName ?? string.Empty,
            GeneratedTestMethodName = execution?.GeneratedTestMethodName ?? string.Empty,
            GeneratedTestCompiled = generatedTestCompiled,
            GeneratedTestExecuted = generatedTestExecuted,
            GeneratedTestPassed = generatedTestPassed,
            CoverageBefore = candidateMethod.BaselineCoverage,
            CoverageAfter = execution?.CoverageAfter ?? 0,
            CoverageDelta = execution?.CoverageImprovement ?? 0,
            MutationScoreBefore = execution?.BaselineMutationScore,
            MutationScoreAfter = execution?.MutationScoreAfter,
            MutationScoreDelta = execution?.MutationScoreImprovement,
            MutantKilled = execution?.MutationScoreImprovement is > 0,
            ToolObservedOutcome = execution?.Classification.ToString() ?? TestClassification.ValidationFailed.ToString(),
            AcceptedByNormalPolicy = execution?.Accepted,
            FailureKind = execution?.FailureKind.ToString() ?? string.Empty,
            FailureStage = execution?.FailureStage ?? string.Empty,
            FailureCategory = execution?.FailureCategory ?? string.Empty,
            FailureSummary = execution?.FailureSummary ?? string.Empty,
            RoslynValidationSucceeded = execution?.RoslynValidationSucceeded ?? true,
            RoslynValidationSkipped = execution?.RoslynValidationSkipped ?? false,
            RoslynDiagnosticsBeforeCount = execution?.RoslynDiagnosticsBeforeCount ?? 0,
            RoslynDiagnosticsAfterCount = execution?.RoslynDiagnosticsAfterCount ?? 0,
            NewRoslynDiagnosticsCount = execution?.NewRoslynDiagnosticsCount ?? 0,
            NewRoslynDiagnostics = execution?.NewRoslynDiagnostics ?? string.Empty,
            TotalTokens = attempt.TotalTokensUsed,
            CumulativeTokens = attempt.ChainCumulativeTokensUsed > 0
                ? attempt.ChainCumulativeTokensUsed
                : attempt.TotalTokensUsed,
            GenerationDurationSeconds = attempt.GenerationDurationSeconds,
            ValidationDurationSeconds = attempt.ValidationDurationSeconds,
            TotalAttemptDurationSeconds = attempt.TotalDurationSeconds,
            BaselineTestExecutionTimeMs = baselineTestExecutionTimeMs,
            GeneratedTestExecutionTimeMs = execution?.ExecutionTimeMs,
            PromptVersion = attempt.GenerationSteps.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.PromptVersion))?.PromptVersion ?? string.Empty,
            GenerationAttemptId = attempt.Id,
            TestExecutionId = execution?.Id,
            ResumeStableKey = stableKey
        };
    }

    /// <summary>
    /// Builds one CSV row per test result from the post-attempt measurement run.
    /// Falls back to one row per linked test member, then to a single summary row.
    /// Coverage and mutation data are loaded from the targeted-baseline and post-attempt
    /// test runs so those columns are populated even when the attempt spans multiple tests.
    /// </summary>
    private async Task<IReadOnlyList<ExperimentResultFileRow>> CreateToolResultFileRowsAsync(
        ExperimentRun experimentRun,
        CandidateMethod candidateMethod,
        CandidateMethodContext methodContext,
        ExperimentMatrixWorkItem workItem,
        ToolAttempt attempt,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        var sourceMetrics = await GetMemberCodeMetricsAsync(candidateMethod.MemberId, cancellationToken);
        var baselineMetrics = await GetMemberCodeMetricsAsync(candidateMethod.ExistingTestMemberId, cancellationToken);
        var baselineTestSmells = await GetTestSmellSummaryAsync(
            candidateMethod.ExistingTestMethodName,
            candidateMethod.ExistingTestMemberId,
            cancellationToken);
        var testability = methodContext.Testability;
        var selectedAccessPath = testability?.AccessPaths.FirstOrDefault();

        // Load mutation from the targeted baseline and post-measurement runs. Coverage columns
        // are method-scoped, so use the candidate baseline and the measured member coverage
        // instead of aggregate test-project coverage.
        var (_, baselineMutation) =
            await GetTestRunMetricsAsync(attempt.TargetedBaselineId, cancellationToken);
        var (postCoverage, postMutation) =
            await GetTestRunMetricsAsync(attempt.PostAttemptTestRunId, cancellationToken);
        var postMethodCoverage = await GetMemberCoverageForTestRunAsync(
            attempt.PostAttemptTestRunId,
            candidateMethod.MemberId,
            cancellationToken);

        var coverageBefore = candidateMethod.BaselineCoverage;
        var coverageAfter = postMethodCoverage ?? postCoverage ?? 0.0;
        var coverageDelta = coverageAfter - coverageBefore;
        double? mutationDelta = (postMutation.HasValue && baselineMutation.HasValue)
            ? postMutation.Value - baselineMutation.Value
            : null;

        // Whether the tests compiled/ran is derived from the validation outcome.
        // Tests failing still means the code compiled and the tests executed.
        var compiled = attempt.PostAttemptTestRunId.HasValue
                       || attempt.ValidationOutcome == ToolValidationOutcome.TestsFailed;
        var ran = attempt.PostAttemptTestRunId.HasValue;
        var allPassed = attempt.ValidationOutcome == ToolValidationOutcome.Passed;

        // Pre-fetch linked test members with IDs so code metrics and test smells can be
        // looked up per-test-method and written into each row.
        var linkedMembers = await GetLinkedTestMembersAsync(attempt.Id, cancellationToken);
        var metricsByMemberId = new Dictionary<int, MemberCodeMetricColumns?>(linkedMembers.Count);
        var smellsByMemberId = new Dictionary<int, string>(linkedMembers.Count);
        foreach (var (memberId, memberName) in linkedMembers)
        {
            metricsByMemberId[memberId] = await GetMemberCodeMetricsAsync(memberId, cancellationToken);
            smellsByMemberId[memberId] = await GetTestSmellSummaryAsync(memberName, memberId, cancellationToken);
        }

        // Match a test result name (possibly fully-qualified, e.g. "ClassName.MethodName")
        // back to a linked member ID by checking equality then suffix.
        // [Theory] tests append parameter values in parens ("TestMethod(x: 1)") so strip
        // everything from the first '(' before comparing against the bare member name.
        int? FindMemberId(string testName)
        {
            return ResolveLinkedMemberId(testName, linkedMembers);
        }

        // Reusable factory for the shared fields. Looks up generated-test code metrics and
        // test smells from the pre-fetched dictionaries using the resolved member ID.
        ExperimentResultFileRow MakeRow(
            string testName,
            bool testCompiled,
            bool testExecuted,
            bool testPassed,
            double? executionTimeMs = null)
        {
            var memberId = FindMemberId(testName);
            var generatedMetrics = memberId.HasValue && metricsByMemberId.TryGetValue(memberId.Value, out var gm) ? gm : null;
            var generatedSmells = memberId.HasValue && smellsByMemberId.TryGetValue(memberId.Value, out var gs) ? gs : string.Empty;
            return new()
            {
                ExperimentRunId = experimentRun.Id,
                ProducerLane = "agent-tool",
                ToolId = attempt.ToolId,
                ToolRunStatus = attempt.RunStatus.ToString(),
                ToolValidationOutcome = attempt.ValidationOutcome.ToString(),
                ToolArtifactPath = attempt.ArtifactPath,
                ToolChangedFilesCount = attempt.ChangedFilesCount,
                ToolAttemptId = attempt.Id,
                ToolAttemptTargetedBaselineId = attempt.TargetedBaselineId,
                ToolPostAttemptTestRunId = attempt.PostAttemptTestRunId,
                RepoUrl = _context.Project.GitHubUrl,
                RepoOwner = _context.Project.Owner,
                RepoName = _context.Project.RepoName,
                CommitHash = _context.Project.Commit ?? _context.Project.LastAnalyzedCommit ?? _context.CurrentCommit ?? string.Empty,
                RunDate = DateTime.UtcNow,
                Objective = experimentRun.Objective,
                TargetSelectionStrategy = experimentRun.CandidateSelectionStrategy,
                GenerationApproach = workItem.Approach,
                MetricsPath = workItem.MetricsPath,
                SourceMethodMaintainabilityIndex = sourceMetrics?.MaintainabilityIndex,
                SourceMethodCyclomaticComplexity = sourceMetrics?.CyclomaticComplexity,
                SourceMethodClassCoupling = sourceMetrics?.ClassCoupling,
                SourceMethodDepthOfInheritance = sourceMetrics?.DepthOfInheritance,
                SourceMethodSourceLinesOfCode = sourceMetrics?.SourceLinesOfCode,
                SourceMethodExecutableLinesOfCode = sourceMetrics?.ExecutableLinesOfCode,
                BaselineTestMaintainabilityIndex = baselineMetrics?.MaintainabilityIndex,
                BaselineTestCyclomaticComplexity = baselineMetrics?.CyclomaticComplexity,
                BaselineTestClassCoupling = baselineMetrics?.ClassCoupling,
                BaselineTestDepthOfInheritance = baselineMetrics?.DepthOfInheritance,
                BaselineTestSourceLinesOfCode = baselineMetrics?.SourceLinesOfCode,
                BaselineTestExecutableLinesOfCode = baselineMetrics?.ExecutableLinesOfCode,
                BaselineTestSmells = baselineTestSmells,
                GeneratedTestMaintainabilityIndex = generatedMetrics?.MaintainabilityIndex,
                GeneratedTestCyclomaticComplexity = generatedMetrics?.CyclomaticComplexity,
                GeneratedTestClassCoupling = generatedMetrics?.ClassCoupling,
                GeneratedTestDepthOfInheritance = generatedMetrics?.DepthOfInheritance,
                GeneratedTestSourceLinesOfCode = generatedMetrics?.SourceLinesOfCode,
                GeneratedTestExecutableLinesOfCode = generatedMetrics?.ExecutableLinesOfCode,
                GeneratedTestSmells = generatedSmells,
                Provider = workItem.Provider,
                Model = workItem.ModelName,
                ContextMode = workItem.ContextMode,
                BudgetMode = workItem.BudgetMode,
                AblationVariantId = workItem.AblationVariantId,
                StepsIncluded = workItem.StepConfigJson,
                SourceMemberVisibility = testability?.Visibility.ToString() ?? string.Empty,
                AccessStrategy = selectedAccessPath?.Strategy.ToString() ?? string.Empty,
                AccessPathMemberIds = selectedAccessPath == null
                    ? string.Empty
                    : string.Join(">", selectedAccessPath.PathMemberIds),
                TestMappingCount = testability?.TestMappings.Count ?? 0,
                SetupBindingCount = testability?.SetupBindings.Count ?? 0,
                AttemptNumber = attemptNumber,
                SourceMemberId = candidateMethod.MemberId,
                SourceMethodName = candidateMethod.MethodName,
                SourceMethodSignature = candidateMethod.Signature,
                CandidateTestIntentionsSummary = candidateMethod.TestIntentionsSummary,
                CandidateTypeConstructionSummary = candidateMethod.TypeConstructionSummary,
                CandidateMetadataJson = candidateMethod.CandidateMetadataJson,
                SourceMethodBaselineCoverage = candidateMethod.BaselineCoverage,
                SourceMethodComplexity = candidateMethod.ComplexityScore,
                BaselineTestState = candidateMethod.TestState.ToString(),
                BaselineTestMethod = candidateMethod.ExistingTestMethodName ?? string.Empty,
                GeneratedTestMethodName = testName,
                GeneratedTestCompiled = testCompiled,
                GeneratedTestExecuted = testExecuted,
                GeneratedTestPassed = testPassed,
                CoverageBefore = coverageBefore,
                CoverageAfter = coverageAfter,
                CoverageDelta = coverageDelta,
                MutationScoreBefore = baselineMutation,
                MutationScoreAfter = postMutation,
                MutationScoreDelta = mutationDelta,
                MutantKilled = mutationDelta is > 0,
                ToolObservedOutcome = attempt.ObservedOutcome.ToString(),
                AcceptedByNormalPolicy = null,
                FailureKind = attempt.RunStatus is ToolRunStatus.Completed or ToolRunStatus.CompletedNoChange or ToolRunStatus.Skipped
                    ? string.Empty
                    : "ToolExecution",
                FailureStage = attempt.RunStatus is ToolRunStatus.Completed or ToolRunStatus.CompletedNoChange or ToolRunStatus.Skipped
                    ? string.Empty
                    : "tool",
                FailureCategory = attempt.RunStatus.ToString(),
                FailureSummary = attempt.Notes,
                RoslynValidationSucceeded = false,
                RoslynValidationSkipped = true,
                TotalTokens = (attempt.InputTokens ?? 0) + (attempt.OutputTokens ?? 0),
                GenerationDurationSeconds = attempt.GenerationDurationSeconds,
                ValidationDurationSeconds = attempt.ValidationDurationSeconds,
                TotalAttemptDurationSeconds = attempt.TotalAttemptDurationSeconds,
                GeneratedTestExecutionTimeMs = executionTimeMs,
                PromptVersion = "agent-tool-task-card",
                GenerationAttemptId = 0,
                TestExecutionId = null,
                ResumeStableKey = workItem.StableKey
            };
        }

        // Primary: one row per individual test result for per-test pass/fail granularity.
        if (attempt.PostAttemptTestRunId.HasValue)
        {
            var testResults = await GetPostAttemptTestResultsAsync(
                attempt.PostAttemptTestRunId.Value, cancellationToken);
            var testDurations = await GetPostAttemptTestDurationsAsync(
                attempt.PostAttemptTestRunId.Value, cancellationToken);
            var generatedTestResults = SelectGeneratedPostAttemptTestResults(testResults, linkedMembers);
            if (generatedTestResults.Count > 0)
                return generatedTestResults
                    .Select(tr => MakeRow(
                        tr.TestName,
                        compiled,
                        true,
                        string.Equals(tr.Outcome, "Passed", StringComparison.OrdinalIgnoreCase),
                        testDurations.GetValueOrDefault(tr.TestName)))
                    .ToList();
        }

        // Fallback: one row per linked test member (analysis-time names).
        if (linkedMembers.Count > 0)
            return linkedMembers.Select(m => MakeRow(m.Name, compiled, ran, allPassed)).ToList();

        // Final fallback: single summary row with no per-test name.
        return [MakeRow(string.Empty, compiled, ran, allPassed)];
    }

    internal static List<(string TestName, string Outcome)> SelectGeneratedPostAttemptTestResults(
        IReadOnlyList<(string TestName, string Outcome)> testResults,
        IReadOnlyList<(int MemberId, string Name)> linkedMembers)
    {
        if (testResults.Count == 0 || linkedMembers.Count == 0)
            return [];

        return testResults
            .Where(x => ResolveLinkedMemberId(x.TestName, linkedMembers).HasValue)
            .ToList();
    }

    internal static int? ResolveLinkedMemberId(
        string testName,
        IReadOnlyList<(int MemberId, string Name)> linkedMembers)
    {
        if (string.IsNullOrEmpty(testName)) return null;
        var bare = StripTestArguments(testName);
        foreach (var (memberId, memberName) in linkedMembers)
        {
            if (string.Equals(bare, memberName, StringComparison.OrdinalIgnoreCase) ||
                bare.EndsWith("." + memberName, StringComparison.OrdinalIgnoreCase))
                return memberId;
        }

        return null;
    }

    /// <summary>
    /// Returns the aggregate coverage line rate and mutation score for a given test run.
    /// Returns (null, null) when <paramref name="testRunId"/> is null or no reports are found.
    /// </summary>
    internal async Task<(double? Coverage, double? MutationScore)> GetTestRunMetricsAsync(
        int? testRunId,
        CancellationToken cancellationToken)
    {
        if (!testRunId.HasValue) return (null, null);

        var coverage = await _dbContext.CoverageReports
            .Where(x => x.TestRunId == testRunId.Value)
            .OrderByDescending(x => x.Id)
            .Select(x => (double?)x.LineRate)
            .FirstOrDefaultAsync(cancellationToken);

        var mutation = await _dbContext.MutationTestingReports
            .Where(x => x.TestRunId == testRunId.Value)
            .OrderByDescending(x => x.Id)
            .Select(x => (double?)x.MutationScore)
            .FirstOrDefaultAsync(cancellationToken);

        return (coverage, mutation);
    }

    internal async Task<double?> GetMemberCoverageForTestRunAsync(
        int? testRunId,
        int memberId,
        CancellationToken cancellationToken)
    {
        if (!testRunId.HasValue || memberId <= 0) return null;

        return await (
            from report in _dbContext.CoverageReports
            join memberCoverage in _dbContext.MemberCoverages on report.Id equals memberCoverage.CoverageReportId
            where report.TestRunId == testRunId.Value && memberCoverage.MemberId == memberId
            orderby report.Id descending, memberCoverage.Id descending
            select (double?)memberCoverage.LineRate
        ).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Returns all test results for a post-attempt test run as (TestName, Outcome) pairs.
    /// </summary>
    internal async Task<List<(string TestName, string Outcome)>> GetPostAttemptTestResultsAsync(
        int testRunId,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.TestResults
            .Where(x => x.TestRunId == testRunId)
            .OrderBy(x => x.TestName)
            .Select(x => new { x.TestName, x.Outcome })
            .ToListAsync(cancellationToken);
        return rows.Select(x => (x.TestName, x.Outcome)).ToList();
    }

    internal async Task<Dictionary<string, double>> GetPostAttemptTestDurationsAsync(
        int testRunId,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.TestResults
            .Where(x => x.TestRunId == testRunId)
            .Select(x => new { x.TestName, x.Duration })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.TestName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(row => row.Duration.TotalMilliseconds),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the member IDs and names of test members linked to a tool attempt via
    /// <c>tool_attempt_generated_tests</c>, ordered by name.
    /// </summary>
    internal async Task<List<(int MemberId, string Name)>> GetLinkedTestMembersAsync(
        int toolAttemptId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from tag in _dbContext.ToolAttemptGeneratedTests
            join member in _dbContext.Members on tag.MemberId equals member.Id
            where tag.ToolAttemptId == toolAttemptId
            orderby member.Name
            select new { tag.MemberId, member.Name }
        ).ToListAsync(cancellationToken);
        return rows.Select(x => (x.MemberId, x.Name)).ToList();
    }

    /// <summary>
    /// Returns the member names of test members linked to a tool attempt via
    /// <c>tool_attempt_generated_tests</c>.
    /// </summary>
    internal async Task<List<string>> GetLinkedTestMemberNamesAsync(
        int toolAttemptId,
        CancellationToken cancellationToken)
    {
        var members = await GetLinkedTestMembersAsync(toolAttemptId, cancellationToken);
        return members.Select(x => x.Name).ToList();
    }

    private static AccessPathReport ResolveAccessPathReport(string? decisionJson)
    {
        var decision = ParseRuleDecisions(decisionJson)
            .FirstOrDefault(x => x.RuleId == GenerationExperimentRuleDefinitions.ContextAccessPathSelected.Id);
        if (decision == null) return AccessPathReport.Empty;

        return new AccessPathReport(
            EvidenceValue(decision, "visibility"),
            EvidenceValue(decision, "access_strategy", decision.Value),
            EvidenceValue(decision, "path_member_ids"),
            ParseIntEvidence(decision, "mapping_count"),
            ParseIntEvidence(decision, "setup_binding_count"));
    }

    /// <summary>
    /// Strips display arguments appended by test frameworks to parameterized test names.
    /// xUnit [Theory] tests write names like "Ns.Class.Method(x: 1, y: 2)" into TRX files;
    /// stripping to "Ns.Class.Method" lets the name match the bare Roslyn member name.
    /// </summary>
    internal static string StripTestArguments(string testName)
    {
        var idx = testName.IndexOf('(');
        return idx >= 0 ? testName[..idx].TrimEnd() : testName;
    }

    private static string EvidenceValue(
        RuleDecisionRecord decision,
        string key,
        string fallback = "")
    {
        return decision.Evidence.FirstOrDefault(x =>
            string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.Value ?? fallback;
    }

    private static int ParseIntEvidence(RuleDecisionRecord decision, string key)
    {
        return int.TryParse(EvidenceValue(decision, key), out var value) ? value : 0;
    }

    private sealed record AccessPathReport(
        string Visibility,
        string AccessStrategy,
        string PathMemberIds,
        int MappingCount,
        int SetupBindingCount)
    {
        public static AccessPathReport Empty { get; } = new(string.Empty, string.Empty, string.Empty, 0, 0);
    }

    private async Task<int?> ResolveLatestTestMemberIdAsync(
        string? testName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(testName)) return null;

        return await (
                from member in _dbContext.Members
                where member.IsTestMember
                      && (
                          member.Name == testName ||
                          EF.Functions.Like(member.Name, "%" + testName))
                orderby member.Id descending
                select (int?)member.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the most recent recorded execution duration for the baseline test, in milliseconds.
    /// Looks up by member ID first (exact), then falls back to a name prefix match.
    /// Returns null when no data is available.
    /// </summary>
    private async Task<double?> GetBaselineTestExecutionTimeMsAsync(
        string? testMethodName,
        int? memberId,
        CancellationToken cancellationToken)
    {
        if (_context.Project.DbId == 0 || (string.IsNullOrWhiteSpace(testMethodName) && !memberId.HasValue))
            return null;

        if (memberId.HasValue)
        {
            var byMemberId =
                from testResult in _dbContext.TestResults
                join testRun in _dbContext.TestRuns on testResult.TestRunId equals testRun.Id
                where testRun.ProjectId == _context.Project.DbId
                      && testResult.MethodId == memberId.Value
                orderby testRun.Id descending, testResult.Id descending
                select (double?)testResult.Duration.TotalMilliseconds;

            var resultByMemberId = await byMemberId.FirstOrDefaultAsync(cancellationToken);
            if (resultByMemberId.HasValue) return resultByMemberId;
        }

        if (string.IsNullOrWhiteSpace(testMethodName)) return null;

        return await (
                from testResult in _dbContext.TestResults
                join testRun in _dbContext.TestRuns on testResult.TestRunId equals testRun.Id
                where testRun.ProjectId == _context.Project.DbId
                      && (
                          testResult.TestName == testMethodName ||
                          EF.Functions.Like(testResult.TestName, "%." + testMethodName) ||
                          EF.Functions.Like(testResult.TestName, "%+" + testMethodName))
                orderby testRun.Id descending, testResult.Id descending
                select (double?)testResult.Duration.TotalMilliseconds)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<MemberCodeMetricColumns?> GetMemberCodeMetricsAsync(
        int? memberId,
        CancellationToken cancellationToken)
    {
        if (!memberId.HasValue) return null;

        var metric = await _dbContext.CodeMetrics
            .Where(x => x.EntityType == "member" && x.EntityId == memberId.Value)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return metric == null
            ? null
            : new MemberCodeMetricColumns(
                metric.MaintainabilityIndex,
                metric.CyclomaticComplexity,
                metric.ClassCoupling,
                metric.DepthOfInheritance,
                metric.SourceLinesOfCode,
                metric.ExecutableLinesOfCode);
    }

    private async Task<string> GetTestSmellSummaryAsync(
        string? testName,
        int? memberId,
        CancellationToken cancellationToken)
    {
        if (_context.Project.DbId == 0 && !memberId.HasValue && string.IsNullOrWhiteSpace(testName))
            return string.Empty;

        var query = _dbContext.TestSmells.AsQueryable();

        if (memberId.HasValue)
            query = query.Where(x => x.MemberId == memberId.Value);
        else if (!string.IsNullOrWhiteSpace(testName))
            query = query.Where(x => x.TestMethodName == testName);
        else
            return string.Empty;

        var smells = await query
            .Where(x => _context.Project.DbId == 0 || x.ProjectId == _context.Project.DbId)
            .GroupBy(x => x.SmellName)
            .Select(x => new { Name = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return smells.Count == 0
            ? "None"
            : string.Join("; ", smells.Select(x => $"{x.Name}={x.Count}"));
    }

    private static string ResolveResultsFilePath(ExperimentConfig config)
    {
        return ExperimentResultsWriter.ResolveResultsFilePath(config);
    }

    public async Task<IReadOnlyList<GenerationAttempt>> ExecuteGenerationAttemptAsync(
        CandidateMethod candidateMethod,
        CandidateMethodContext context,
        AiProvider provider,
        TestMap.Models.Configuration.Testing.Generation.GenerationBudgetMode budgetMode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var activeApproach = _activeExperimentConfig?.Approaches is { Count: > 0 } a
                ? a[0]
                : TestMap.Models.Configuration.Testing.Generation.TestGenerationApproach.MetricsDriven;
            var item = new GenerationExperimentMatrixItem
            {
                VariantId = $"{provider}__{activeApproach}__{budgetMode}__baseline",
                Provider = provider,
                ModelName = ResolveModelName(provider),
                Approach = activeApproach,
                MetricsPath = _activeExperimentConfig?.MetricsPaths.FirstOrDefault(),
                ContextMode = _config.TestingConfig.GenerationConfig.ContextMode,
                BudgetMode = budgetMode,
                Steps = _config.TestingConfig.GenerationConfig.Steps,
                Temperature = _activeExperimentConfig?.Temperature ?? 0.0
            };
            item = new GenerationExperimentMatrixItem
            {
                VariantId = item.VariantId,
                Provider = item.Provider,
                ModelName = item.ModelName,
                Approach = item.Approach,
                MetricsPath = item.MetricsPath,
                ContextMode = item.ContextMode,
                BudgetMode = item.BudgetMode,
                Steps = item.Steps,
                Temperature = item.Temperature,
                EffectiveProfile = GenerationProfileResolver.ResolveEffectiveProfile(
                    _config.TestingConfig.GenerationConfig,
                    _activeExperimentConfig ?? new ExperimentConfig(),
                    item)
            };

            return await ExecuteGenerationAttemptAsync(candidateMethod.Id, context, item, cancellationToken);
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error(ex, "Generation attempt failed.");
            return [CreateFailedAttempt(candidateMethod.Id, provider, budgetMode, 1, ex.Message)];
        }
    }

    private Task<IReadOnlyList<GenerationAttempt>> ExecuteGenerationAttemptAsync(
        int candidateMethodId,
        CandidateMethodContext context,
        GenerationExperimentMatrixItem matrixItem,
        CancellationToken cancellationToken = default)
    {
        return ExecuteBudgetAsync(candidateMethodId, context, matrixItem, cancellationToken);
    }

    private async Task<IReadOnlyList<GenerationAttempt>> ExecuteBudgetAsync(
        int candidateMethodId,
        CandidateMethodContext context,
        GenerationExperimentMatrixItem matrixItem,
        CancellationToken cancellationToken)
    {
        // Accumulates every completed attempt in chain order so that each repair can
        // receive a compact history of all prior failures (not just the previous one).
        var repairHistory = new List<GenerationAttempt>();

        var evaluations = await _budgetExecutor.ExecuteAsync(
            new GenerationBudgetExecutionRequest
            {
                BudgetMode = matrixItem.BudgetMode,
                GenerateAsync = async (attemptNumber, token) =>
                {
                    var attempt = await ExecuteSingleGenerationAttemptAsync(candidateMethodId, context, matrixItem, attemptNumber, token);
                    repairHistory.Add(attempt);
                    return attempt;
                },
                RepairAsync = async (previousAttempt, attemptNumber, token) =>
                {
                    var priorSnapshot = repairHistory.ToList();
                    var attempt = await ExecuteSingleRepairAttemptAsync(candidateMethodId, context, matrixItem, previousAttempt, priorSnapshot, attemptNumber, token);
                    repairHistory.Add(attempt);
                    return attempt;
                },
                ShouldStopRepair = attempt =>
                    attempt.TestExecution is { TestPassed: true, CoverageImprovement: > 0 },
                RollbackAsync = token => _workspace.RollbackChangesAsync(token)
            },
            cancellationToken);

        return evaluations.Select(x =>
        {
            x.Attempt.IsRepairAttempt = x.IsRepairAttempt;
            x.Attempt.ParentAttemptNumber = x.ParentAttemptNumber;
            return x.Attempt;
        }).ToList();
    }

    private async Task<GenerationAttempt> ExecuteSingleGenerationAttemptAsync(
        int candidateMethodId,
        CandidateMethodContext context,
        GenerationExperimentMatrixItem matrixItem,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
            var attempt = CreateAttempt(
                candidateMethodId,
                matrixItem.Provider,
                matrixItem.BudgetMode,
                attemptNumber,
                context,
                matrixItem);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            TestGenerationResult result;
            var generationStopwatch = Stopwatch.StartNew();
            try
            {
                result = await _pipeline.GenerateTestAsync(
                    CreateGenerationRequest(context, matrixItem),
                    cancellationToken);
            }
            finally
            {
                generationStopwatch.Stop();
                attempt.GenerationDurationSeconds = generationStopwatch.Elapsed.TotalSeconds;
            }

            attempt.GenerationSteps = MapSteps(result);
            attempt.TotalTokensUsed = result.TotalTokens;
            attempt.ConversationTranscript = result.ConversationTranscript;

            if (!result.Success || string.IsNullOrEmpty(result.GeneratedTest))
            {
                attempt.ErrorMessage = result.ErrorMessage ?? "Generation failed";
                attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, attempt.ErrorMessage);
                return attempt;
            }

            var validationStopwatch = Stopwatch.StartNew();
            try
            {
                attempt.TestExecution = await ExecuteAndTestAsync(
                    result.GeneratedTest!,
                    result.TestMethodName!,
                    context,
                    matrixItem,
                    cancellationToken);
            }
            finally
            {
                validationStopwatch.Stop();
                attempt.ValidationDurationSeconds = validationStopwatch.Elapsed.TotalSeconds;
            }

            // Persist patch metadata on the attempt (Phase 4).
            // PatchJson is the raw generated test string (JSON for BasicExtension, C# for legacy).
            attempt.PatchJson = result.GeneratedTest;
            attempt.PatchApplicationOutcome = attempt.TestExecution?.PatchApplicationOutcome;
            attempt.AppliedUsingCount = attempt.TestExecution?.AppliedUsingCount ?? 0;
            attempt.AppliedHelperCount = attempt.TestExecution?.AppliedHelperCount ?? 0;

            await CaptureModifiedFileAsync(attempt, context.TestFilePath, cancellationToken);

            return attempt;
        }
        finally
        {
            stopwatch.Stop();
            attempt.CompletedAt = DateTime.UtcNow;
            attempt.TotalDurationSeconds = stopwatch.Elapsed.TotalSeconds;
        }
    }

    private async Task<GenerationAttempt> ExecuteSingleRepairAttemptAsync(
        int candidateMethodId,
        CandidateMethodContext context,
        GenerationExperimentMatrixItem matrixItem,
        GenerationAttempt previousAttempt,
        IReadOnlyList<GenerationAttempt> allPriorAttempts,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        var attempt = CreateAttempt(
            candidateMethodId,
            matrixItem.Provider,
            matrixItem.BudgetMode,
            attemptNumber,
            context,
            matrixItem);
        attempt.IsRepairAttempt = true;
        attempt.ParentAttemptNumber = previousAttempt.AttemptNumber;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (previousAttempt.TestExecution?.GeneratedTestCode == null)
            {
                attempt.ErrorMessage = "No previous attempt available for repair.";
                attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, attempt.ErrorMessage);
                return attempt;
            }

            // Build a compact failure history from all prior attempts so the model
            // knows which approaches have already been tried and should not be repeated.
            var priorAttemptsSummary = BuildPriorAttemptsSummary(allPriorAttempts);

            var repairRequest = CreateRepairRequest(
                context,
                previousAttempt.TestExecution.GeneratedTestCode,
                previousAttempt.TestExecution.ErrorLogs ?? "Test failed",
                previousAttempt.TestExecution.StructuredErrors,
                priorAttemptsSummary,
                matrixItem,
                attemptNumber,
                modifiedTestFileContents: previousAttempt.ModifiedFileContents);

            TestGenerationResult result;
            var generationStopwatch = Stopwatch.StartNew();
            try
            {
                result = await _pipeline.RepairTestAsync(repairRequest, cancellationToken);
            }
            finally
            {
                generationStopwatch.Stop();
                attempt.GenerationDurationSeconds = generationStopwatch.Elapsed.TotalSeconds;
            }

            attempt.GenerationSteps = MapSteps(result);
            attempt.TotalTokensUsed = result.TotalTokens;
            attempt.ConversationTranscript = result.ConversationTranscript;

            if (!result.Success || string.IsNullOrEmpty(result.GeneratedTest))
            {
                attempt.ErrorMessage = result.ErrorMessage ?? $"Repair {attemptNumber} failed";
                attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, attempt.ErrorMessage);
                return attempt;
            }

            var validationStopwatch = Stopwatch.StartNew();
            try
            {
                attempt.TestExecution = await ExecuteAndTestAsync(
                    result.GeneratedTest!,
                    result.TestMethodName ?? context.Method.MethodName,
                    context,
                    matrixItem,
                    cancellationToken);
            }
            finally
            {
                validationStopwatch.Stop();
                attempt.ValidationDurationSeconds = validationStopwatch.Elapsed.TotalSeconds;
            }

            // Persist repair patch metadata on the attempt (Phase 4).
            attempt.RepairPatchJson = result.GeneratedTest;
            attempt.PatchApplicationOutcome = attempt.TestExecution?.PatchApplicationOutcome;
            attempt.AppliedUsingCount = attempt.TestExecution?.AppliedUsingCount ?? 0;
            attempt.AppliedHelperCount = attempt.TestExecution?.AppliedHelperCount ?? 0;

            await CaptureModifiedFileAsync(attempt, context.TestFilePath, cancellationToken);

            return attempt;
        }
        finally
        {
            stopwatch.Stop();
            attempt.CompletedAt = DateTime.UtcNow;
            attempt.TotalDurationSeconds = stopwatch.Elapsed.TotalSeconds;
        }
    }

    private TestGenerationRequest CreateGenerationRequest(
        CandidateMethodContext context,
        GenerationExperimentMatrixItem matrixItem)
    {
        var experimentConfig = _activeExperimentConfig;

        var request = ResolveGenerationApproach(matrixItem.Approach).CreateGenerationRequest(new TestGenerationApproachContext
        {
            MethodContext = context,
            Provider = matrixItem.Provider,
            Temperature = matrixItem.Temperature,
            StepErrorRetries = Math.Max(0, experimentConfig?.StepErrorRetries ?? 0),
            StepRetryDelayMs = Math.Max(0, experimentConfig?.StepRetryDelayMs ?? 1000)
        });

        return ApplyMatrixItem(request, matrixItem);
    }

    internal static async Task CaptureModifiedFileAsync(
        GenerationAttempt attempt,
        string? filePath,
        CancellationToken cancellationToken = default)
    {
        if (attempt.TestExecution == null ||
            !PatchWasApplied(attempt.TestExecution.PatchApplicationOutcome) ||
            string.IsNullOrWhiteSpace(filePath) ||
            !File.Exists(filePath))
            return;

        var contents = await File.ReadAllTextAsync(filePath, cancellationToken);
        attempt.ModifiedFilePath = Path.GetFullPath(filePath);
        attempt.ModifiedFileContents = contents;
        attempt.ModifiedFileSha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(contents)))
            .ToLowerInvariant();
    }

    private static bool PatchWasApplied(string? patchApplicationOutcome)
    {
        // Legacy method-only insertion does not report a patch outcome.
        return string.IsNullOrWhiteSpace(patchApplicationOutcome) ||
               patchApplicationOutcome.Equals("Success", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds a concise one-line-per-attempt summary of every failure in the repair chain
    /// that occurred <em>before</em> the immediately previous attempt (whose full context is
    /// already in the repair prompt via <c>GeneratedTest</c> and <c>ErrorLogs</c>).
    /// Returns an empty string when there is nothing genuinely new to show (i.e. the first
    /// repair, where the only prior attempt is already fully covered by the prompt).
    /// </summary>
    private static string BuildPriorAttemptsSummary(IReadOnlyList<GenerationAttempt> priorAttempts)
    {
        // Exclude the most recent attempt: its patch is already in GeneratedTest and its
        // failure is already in ErrorLogs / StructuredErrors / ModifiedTestFileContents.
        // Only attempts before it are genuinely new context for the model.
        var earlierAttempts = priorAttempts.Count > 1
            ? priorAttempts.Take(priorAttempts.Count - 1).ToList()
            : [];

        if (earlierAttempts.Count == 0)
            return string.Empty;

        var sb = new StringBuilder("Earlier attempt context (each failed before the current attempt above):");
        foreach (var attempt in earlierAttempts)
        {
            var label = attempt.IsRepairAttempt
                ? $"repair {attempt.AttemptNumber - 1}"
                : "initial generation";

            // Application failures surface via PatchApplicationOutcome (e.g. DuplicateTestMethod);
            // build/runtime/assertion failures surface via FailureKind on the test execution.
            var outcomeDetail = !string.IsNullOrWhiteSpace(attempt.PatchApplicationOutcome) &&
                                !attempt.PatchApplicationOutcome.Equals("Success", StringComparison.OrdinalIgnoreCase)
                ? $"application failure ({attempt.PatchApplicationOutcome})"
                : attempt.TestExecution?.FailureKind switch
                {
                    TestFailureKind.Compilation => "build failure",
                    TestFailureKind.Runtime     => "runtime failure",
                    TestFailureKind.Assertion   => "assertion failure",
                    TestFailureKind.Generation  => "generation failure",
                    _                           => "failure"
                };

            var errorHint = FirstRepairErrorLine(attempt.TestExecution?.ErrorLogs);
            var errorSuffix = string.IsNullOrWhiteSpace(errorHint) ? string.Empty : $" — {errorHint}";
            sb.AppendLine();
            sb.Append($"  Attempt {attempt.AttemptNumber} ({label}): {outcomeDetail}{errorSuffix}");
        }

        return sb.ToString();
    }

    /// <summary>Returns the first non-blank error line truncated to 120 characters.</summary>
    private static string FirstRepairErrorLine(string? errorLogs)
    {
        if (string.IsNullOrWhiteSpace(errorLogs)) return string.Empty;
        var line = errorLogs.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
                   ?? string.Empty;
        return line.Length > 120 ? line[..120] + "…" : line;
    }

    private TestRepairRequest CreateRepairRequest(
        CandidateMethodContext context,
        string generatedTest,
        string errorLogs,
        string? structuredErrors,
        string? priorAttemptsSummary,
        GenerationExperimentMatrixItem matrixItem,
        int attemptNumber,
        double temperature = 0.0,
        string? modifiedTestFileContents = null)
    {
        var experimentConfig = _activeExperimentConfig;

        var request = ResolveGenerationApproach(matrixItem.Approach).CreateRepairRequest(new TestRepairApproachContext
        {
            MethodContext = context,
            GeneratedTest = generatedTest,
            ErrorLogs = errorLogs,
            StructuredErrors = structuredErrors,
            PriorConversationTranscript = null,
            PriorAttemptsSummary = priorAttemptsSummary,
            ModifiedTestFileContents = modifiedTestFileContents,
            Provider = matrixItem.Provider,
            Temperature = matrixItem.Temperature,
            AttemptNumber = attemptNumber,
            StepErrorRetries = Math.Max(0, experimentConfig?.StepErrorRetries ?? 0),
            StepRetryDelayMs = Math.Max(0, experimentConfig?.StepRetryDelayMs ?? 1000)
        });

        return ApplyMatrixItem(request, matrixItem);
    }

    private async Task<ExperimentTestExecution> ExecuteAndTestAsync(
        string generatedTest,
        string testMethodName,
        CandidateMethodContext context,
        GenerationExperimentMatrixItem matrixItem,
        CancellationToken cancellationToken)
    {
        var execution = await _generatedTestExecutionService.ExecuteAsync(
            context,
            generatedTest,
            testMethodName,
            GenerationObjectivePolicy.ResolveExecutor(GetActiveObjective()),
            _activeExperimentRunId,
            cancellationToken);
        var validation = _generationValidationService.Validate(
            execution,
            context,
            CreateValidationEvidence(context, matrixItem));
        var classification = _generationClassificationService.Classify(validation);

        return new ExperimentTestExecution
        {
            GeneratedTestCode = generatedTest,
            GeneratedTestMethodName = testMethodName,
            ExecutedAt = execution.ExecutedAt,
            CompilationSuccess = execution.CompilationSucceeded,
            TestsExecuted = execution.TestsExecuted,
            TestPassed = execution.CompilationSucceeded && execution.TestsExecuted && execution.AllTestsPassed,
            CoverageAfter = execution.CoverageAfter,
            CoverageImprovement = execution.CoverageImprovement,
            BaselineMutationScore = execution.BaselineMutationScore,
            MutationScoreAfter = execution.MutationScoreAfter,
            MutationScoreImprovement = execution.MutationScoreImprovement,
            Classification = MapClassification(classification.Classification),
            ValidationResultJson = JsonSerializer.Serialize(validation),
            ValidationRuleDecisionSnapshotJson = _ruleDecisionRecorder.CreateSnapshotJson(validation.RuleDecisions),
            ClassificationRuleDecisionSnapshotJson = _ruleDecisionRecorder.CreateSnapshotJson(classification.RuleDecisions),
            FailureKind = execution.FailureKind,
            CompilationErrors = execution.CompilationErrors,
            RuntimeErrors = execution.RuntimeErrors,
            AssertionErrors = execution.AssertionErrors,
            StructuredErrors = execution.StructuredErrors,
            ErrorLogs = execution.ErrorLogs,
            FailureStage = execution.FailureStage,
            FailureCategory = execution.FailureCategory,
            FailureSummary = execution.FailureSummary,
            RoslynValidationSucceeded = execution.RoslynValidationSucceeded,
            RoslynValidationSkipped = execution.RoslynValidationSkipped,
            RoslynDiagnosticsBeforeCount = execution.RoslynDiagnosticsBefore.Count,
            RoslynDiagnosticsAfterCount = execution.RoslynDiagnosticsAfter.Count,
            NewRoslynDiagnosticsCount = execution.NewRoslynDiagnostics.Count,
            NewRoslynDiagnostics = FormatRoslynDiagnostics(execution.NewRoslynDiagnostics),
            ExecutionTimeMs = execution.GeneratedTestExecutionTimeMs.HasValue
                ? (long)Math.Round(execution.GeneratedTestExecutionTimeMs.Value)
                : null,
            TestRunId = execution.TestRun?.DbId > 0 ? execution.TestRun.DbId : null,
            // Transient: carry patch metadata back to the orchestrator for attempt-level persistence.
            PatchApplicationOutcome = execution.PatchApplicationOutcome,
            AppliedUsingCount = execution.AppliedUsingCount,
            AppliedHelperCount = execution.AppliedHelperCount
        };
    }

    private GenerationEvidencePackage CreateValidationEvidence(
        CandidateMethodContext context,
        GenerationExperimentMatrixItem matrixItem)
    {
        return new GenerationEvidencePackage
        {
            Objective = GetActiveObjective(),
            Approach = matrixItem.Approach,
            MetricsPath = matrixItem.MetricsPath,
            CandidateContext = context,
            StrategyInstruction = string.Empty
        };
    }

    private static TestClassification MapClassification(
        GeneratedTestClassification classification)
    {
        return classification switch
        {
            GeneratedTestClassification.ValidatedEvidencePositive => TestClassification.ValidatedEvidencePositive,
            GeneratedTestClassification.FailedEvidencePositive => TestClassification.FailedEvidencePositive,
            GeneratedTestClassification.ValidatedLowImpact => TestClassification.ValidatedLowImpact,
            _ => TestClassification.ValidationFailed
        };
    }

    private static string FormatRoslynDiagnostics(IReadOnlyList<RoslynDiagnosticSnapshot> diagnostics)
    {
        if (diagnostics.Count == 0) return string.Empty;

        return string.Join(
            Environment.NewLine,
            diagnostics.Select(x =>
                $"{x.Id} {x.Severity} {x.FilePath ?? string.Empty}({x.StartLine + 1},{x.StartColumn + 1}): {x.Message}"));
    }

    private static TestGenerationRequest ApplyMatrixItem(
        TestGenerationRequest request,
        GenerationExperimentMatrixItem matrixItem)
    {
        return new TestGenerationRequest
        {
            Objective = request.Objective,
            Approach = matrixItem.Approach,
            MetricsPath = matrixItem.MetricsPath,
            ContextMode = matrixItem.ContextMode,
            Steps = matrixItem.Steps,
            ExperimentVariantId = matrixItem.VariantId,
            MethodBody = request.MethodBody,
            MethodName = request.MethodName,
            MethodSignature = request.MethodSignature,
            ContainingClass = request.ContainingClass,
            SourceFilePath = request.SourceFilePath,
            SourceProjectPath = request.SourceProjectPath,
            SolutionFilePath = request.SolutionFilePath,
            SourceStartLine = request.SourceStartLine,
            SourceEndLine = request.SourceEndLine,
            SourceStartPosition = request.SourceStartPosition,
            SourceEndPosition = request.SourceEndPosition,
            ExistingTestFilePath = request.ExistingTestFilePath,
            ExistingTestStartLine = request.ExistingTestStartLine,
            ExistingTestEndLine = request.ExistingTestEndLine,
            ExampleTest = request.ExampleTest,
            ExampleTestMetadataSummary = request.ExampleTestMetadataSummary,
            ProjectTestMetadataSummary = request.ProjectTestMetadataSummary,
            TestClass = request.TestClass,
            TestFileContents = request.TestFileContents,
            TestSupportContext = request.TestSupportContext,
            TestFramework = request.TestFramework,
            TestDependencies = request.TestDependencies,
            CoverageGapSummary = request.CoverageGapSummary,
            MutationSummary = request.MutationSummary,
            CandidateTestIntentionsSummary = request.CandidateTestIntentionsSummary,
            CandidateTypeConstructionSummary = request.CandidateTypeConstructionSummary,
            AccessPathSummary = request.AccessPathSummary,
            UseStructuredPatchOutput = request.UseStructuredPatchOutput,
            TestProjectPath = request.TestProjectPath,
            Provider = request.Provider,
            Temperature = request.Temperature,
            StepErrorRetries = request.StepErrorRetries,
            StepRetryDelayMs = request.StepRetryDelayMs
        };
    }

    private static TestRepairRequest ApplyMatrixItem(
        TestRepairRequest request,
        GenerationExperimentMatrixItem matrixItem)
    {
        return new TestRepairRequest
        {
            Objective = request.Objective,
            Approach = matrixItem.Approach,
            MetricsPath = matrixItem.MetricsPath,
            ContextMode = matrixItem.ContextMode,
            Steps = matrixItem.Steps,
            ExperimentVariantId = matrixItem.VariantId,
            MethodBody = request.MethodBody,
            MethodName = request.MethodName,
            GeneratedTest = request.GeneratedTest,
            TestClass = request.TestClass,
            TestFramework = request.TestFramework,
            TestDependencies = request.TestDependencies,
            TestFileContents = request.TestFileContents,
            TestSupportContext = request.TestSupportContext,
            ExampleTestMetadataSummary = request.ExampleTestMetadataSummary,
            ProjectTestMetadataSummary = request.ProjectTestMetadataSummary,
            CoverageGapSummary = request.CoverageGapSummary,
            MutationSummary = request.MutationSummary,
            CandidateTestIntentionsSummary = request.CandidateTestIntentionsSummary,
            CandidateTypeConstructionSummary = request.CandidateTypeConstructionSummary,
            AccessPathSummary = request.AccessPathSummary,
            UseStructuredPatchOutput = request.UseStructuredPatchOutput,
            ErrorLogs = request.ErrorLogs,
            StructuredErrors = request.StructuredErrors,
            PriorConversationTranscript = request.PriorConversationTranscript,
            PriorAttemptsSummary = request.PriorAttemptsSummary,
            ModifiedTestFileContents = request.ModifiedTestFileContents,
            Provider = request.Provider,
            Temperature = request.Temperature,
            AttemptNumber = request.AttemptNumber,
            StepErrorRetries = request.StepErrorRetries,
            StepRetryDelayMs = request.StepRetryDelayMs
        };
    }

    private List<AiProvider> GetProvidersToTest(ExperimentConfig config)
    {
        var providers = config.IncludeProviders.Any()
            ? config.IncludeProviders.Select(ParseConfiguredProvider).Distinct().ToList()
            : _config.AiProviderConfig.ProviderConfigs
                .Where(AiProviderConfigurationRules.IsUsable)
                .Select(x => x.Provider)
                .Distinct()
                .ToList();

        if (providers.Count == 0)
            throw new InvalidOperationException(
                "No usable AI providers were found in AiProviderConfig. Configure at least one provider before running experiments.");

        if (!string.IsNullOrWhiteSpace(config.PreferredProvider))
        {
            var preferred = ParseConfiguredProvider(config.PreferredProvider);
            if (providers.Remove(preferred))
                providers.Insert(0, preferred);
        }

        return providers;
    }

    private AiProvider ParseConfiguredProvider(string providerName)
    {
        if (!Enum.TryParse<AiProvider>(providerName, true, out var provider))
            throw new InvalidOperationException($"Unknown AI provider '{providerName}'.");

        var providerConfig = _config.AiProviderConfig.GetProviderConfig(provider);
        if (providerConfig == null || !AiProviderConfigurationRules.IsUsable(providerConfig))
        {
            var detail = providerConfig == null
                ? "Provider config section is missing."
                : AiProviderConfigurationRules.GetValidationError(providerConfig) ?? "Provider config is invalid.";
            throw new InvalidOperationException(
                $"Provider '{provider}' is not configured for experiment use. {detail}");
        }

        return provider;
    }

    private string ResolveModelName(AiProvider provider)
    {
        return _config.AiProviderConfig.GetProviderConfig(provider)?.Model ?? string.Empty;
    }

    private async Task<int> SaveGenerationAttemptAsync(
        int experimentRunId,
        int candidateMethodId,
        GenerationAttempt attempt,
        CancellationToken cancellationToken)
    {
        try
        {
            attempt.Id = await _attemptRepo.InsertAsync(attempt, cancellationToken);
            await RecordSnapshotDecisionsAsync(
                RuleDecisionScope.GenerationAttempt(attempt.Id),
                attempt.RuleDecisionSnapshotJson,
                experimentRunId,
                candidateMethodId,
                generationAttemptId: attempt.Id,
                testExecutionId: null,
                cancellationToken);

            foreach (var step in attempt.GenerationSteps)
            {
                step.GenerationAttemptId = attempt.Id;
                step.Id = await _stepRepo.InsertAsync(step, cancellationToken);
                await RecordSnapshotDecisionsAsync(
                    RuleDecisionScope.GenerationStep(step.Id),
                    step.RuleDecisionSnapshotJson,
                    experimentRunId,
                    candidateMethodId,
                    generationAttemptId: attempt.Id,
                    testExecutionId: null,
                    cancellationToken);
            }

            if (attempt.TestExecution != null)
            {
                attempt.TestExecution.GenerationAttemptId = attempt.Id;
                attempt.TestExecution.Id = await _executionRepo.InsertAsync(attempt.TestExecution, cancellationToken);
                var executionDecisions = ParseRuleDecisions(attempt.TestExecution.ValidationRuleDecisionSnapshotJson)
                    .Concat(ParseRuleDecisions(attempt.TestExecution.ClassificationRuleDecisionSnapshotJson))
                    .ToList();
                await _ruleDecisionRecorder.RecordAsync(
                    _context.Project.DbId,
                    RuleDecisionScope.TestExecution(attempt.TestExecution.Id),
                    executionDecisions,
                    experimentRunId,
                    candidateMethodId,
                    attempt.Id,
                    attempt.TestExecution.Id,
                    cancellationToken);
            }

            return attempt.Id;
        }
        catch (DbUpdateException ex)
        {
            var detail = BuildPersistenceErrorDetails(ex);
            _context.Project.Logger?.Error(
                "Failed to persist generation attempt {CandidateMethodId}/{Provider}/{BudgetMode}/{AttemptNumber}: {Details}",
                attempt.CandidateMethodId,
                attempt.Provider,
                attempt.BudgetMode,
                attempt.AttemptNumber,
                detail);
            throw new InvalidOperationException(
                $"Failed to persist generation attempt {attempt.CandidateMethodId}/{attempt.Provider}/{attempt.BudgetMode}/{attempt.AttemptNumber}: {detail}",
                ex);
        }
    }

    private async Task RecordSnapshotDecisionsAsync(
        RuleDecisionScope scope,
        string decisionJson,
        int experimentRunId,
        int candidateMethodId,
        int? generationAttemptId,
        int? testExecutionId,
        CancellationToken cancellationToken)
    {
        await _ruleDecisionRecorder.RecordAsync(
            _context.Project.DbId,
            scope,
            ParseRuleDecisions(decisionJson),
            experimentRunId,
            candidateMethodId,
            generationAttemptId,
            testExecutionId,
            cancellationToken);
    }

    private static IReadOnlyList<TestMap.Models.Rules.RuleDecisionRecord> ParseRuleDecisions(string? decisionJson)
    {
        if (string.IsNullOrWhiteSpace(decisionJson)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<TestMap.Models.Rules.RuleDecisionRecord>>(decisionJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task SaveRiskScoreAsync(CandidateMethod candidateMethod, CancellationToken cancellationToken)
    {
        if (!candidateMethod.RiskScore.HasValue) return;

        await _riskScoreRepo.InsertAsync(
            new MethodRiskScore
            {
                CandidateMethodId = candidateMethod.Id,
                MemberId = candidateMethod.MemberId,
                RiskScore = candidateMethod.RiskScore.Value,
                FactorScores = candidateMethod.RiskFactorScores,
                Weights = candidateMethod.RiskWeights,
                SelectionReason = candidateMethod.RiskSelectionReason,
                CreatedAt = candidateMethod.SelectionTime == default ? DateTime.UtcNow : candidateMethod.SelectionTime
            },
            cancellationToken);
    }

    private GenerationAttempt CreateAttempt(
        int candidateMethodId,
        AiProvider provider,
        TestMap.Models.Configuration.Testing.Generation.GenerationBudgetMode budgetMode,
        int attemptNumber,
        CandidateMethodContext? context = null,
        GenerationExperimentMatrixItem? matrixItem = null)
    {
        return new GenerationAttempt
        {
            CandidateMethodId = candidateMethodId,
            Provider = provider,
            ModelName = matrixItem?.ModelName ?? ResolveModelName(provider),
            Objective = _activeExperimentConfig?.Objective
                        ?? TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective.TestSuiteExpansion,
            GenerationApproach = matrixItem?.Approach
                                 ?? TestMap.Models.Configuration.Testing.Generation.TestGenerationApproach.MetricsDriven,
            MetricsPath = matrixItem?.MetricsPath,
            ContextMode = matrixItem?.ContextMode
                          ?? _config.TestingConfig.GenerationConfig.ContextMode,
            BudgetMode = matrixItem?.BudgetMode ?? budgetMode,
            AblationVariantId = matrixItem?.Steps.VariantId ?? "baseline",
            StepConfigJson = matrixItem?.Steps == null
                ? string.Empty
                : JsonSerializer.Serialize(matrixItem.Steps),
            EffectiveProfileJson = matrixItem?.EffectiveProfile?.ToStableJson() ?? string.Empty,
            EffectiveProfileHash = matrixItem?.EffectiveProfile?.ToStableHash() ?? string.Empty,
            Temperature = matrixItem?.Temperature ?? _activeExperimentConfig?.Temperature ?? 0.0,
            AttemptNumber = attemptNumber,
            RuleDecisionSnapshotJson = BuildContextGraphDecisionSnapshot(context),
            StartedAt = DateTime.UtcNow
        };
    }

    private string BuildContextGraphDecisionSnapshot(CandidateMethodContext? context)
    {
        if (context?.Testability == null) return string.Empty;

        var testability = context.Testability;
        var selectedAccessPath = testability.AccessPaths.FirstOrDefault();
        var evidence = new List<RuleEvidenceRecord>
        {
            RuleDecisionFactory.CreateEvidence(
                "ContextGraph",
                "source_member_id",
                testability.SourceMemberId.ToString()),
            RuleDecisionFactory.CreateEvidence(
                "ContextGraph",
                "visibility",
                testability.Visibility.ToString()),
            RuleDecisionFactory.CreateEvidence(
                "ContextGraph",
                "evidence_statuses",
                string.Join(",", testability.EvidenceStatuses)),
            RuleDecisionFactory.CreateEvidence(
                "ContextGraph",
                "mapping_count",
                testability.TestMappings.Count.ToString()),
            RuleDecisionFactory.CreateEvidence(
                "ContextGraph",
                "setup_binding_count",
                testability.SetupBindings.Count.ToString()),
            RuleDecisionFactory.CreateEvidence(
                "ContextGraph",
                "access_path_summary",
                context.AccessPathSummary)
        };

        if (selectedAccessPath != null)
            evidence.AddRange(
            [
                RuleDecisionFactory.CreateEvidence(
                    "ContextGraph",
                    "access_strategy",
                    selectedAccessPath.Strategy.ToString()),
                RuleDecisionFactory.CreateEvidence(
                    "ContextGraph",
                    "entrypoint_member_id",
                    selectedAccessPath.EntrypointMemberId.ToString()),
                RuleDecisionFactory.CreateEvidence(
                    "ContextGraph",
                    "target_member_id",
                    selectedAccessPath.TargetMemberId.ToString()),
                RuleDecisionFactory.CreateEvidence(
                    "ContextGraph",
                    "path_member_ids",
                    string.Join(">", selectedAccessPath.PathMemberIds)),
                RuleDecisionFactory.CreateEvidence(
                    "ContextGraph",
                    "legal_from_test",
                    selectedAccessPath.IsLegalFromTest.ToString()),
                RuleDecisionFactory.CreateEvidence(
                    "ContextGraph",
                    "requires_reflection",
                    selectedAccessPath.RequiresReflection.ToString())
            ]);

        var decision = RuleDecisionFactory.CreateDecision(
            "ContextAccessPath",
            selectedAccessPath?.Strategy.ToString() ?? TestAccessStrategy.NotReasonablyTestable.ToString(),
            GenerationExperimentRuleDefinitions.ContextAccessPathSelected,
            selectedAccessPath == null ? RuleConfidence.Low : RuleConfidence.High,
            evidence,
            "Selected context graph access path captured before generation.");

        return _ruleDecisionRecorder.CreateSnapshotJson([decision]);
    }

    private GenerationAttempt CreateFailedAttempt(
        int candidateMethodId,
        AiProvider provider,
        TestMap.Models.Configuration.Testing.Generation.GenerationBudgetMode budgetMode,
        int attemptNumber,
        string errorMessage)
    {
        return new GenerationAttempt
        {
            CandidateMethodId = candidateMethodId,
            Provider = provider,
            Objective = _activeExperimentConfig?.Objective
                        ?? TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective.TestSuiteExpansion,
            GenerationApproach = TestMap.Models.Configuration.Testing.Generation.TestGenerationApproach.MetricsDriven,
            BudgetMode = budgetMode,
            AblationVariantId = "baseline",
            AttemptNumber = attemptNumber,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage,
            TestExecution = CreateFailedExecution(TestFailureKind.Generation, errorMessage)
        };
    }

    private static ExperimentTestExecution CreateFailedExecution(TestFailureKind failureKind, string? errorMessage)
    {
        return new ExperimentTestExecution
        {
            CompilationSuccess = false,
            TestsExecuted = false,
            TestPassed = false,
            FailureKind = failureKind,
            ErrorLogs = errorMessage,
            RuntimeErrors = failureKind == TestFailureKind.Generation ? errorMessage : null,
            CompilationErrors = failureKind == TestFailureKind.Compilation ? errorMessage : null,
            FailureStage = failureKind == TestFailureKind.Generation ? "generation" : "execution",
            FailureCategory = failureKind.ToString(),
            FailureSummary = errorMessage
        };
    }

    private static List<GenerationStep> MapSteps(TestGenerationResult result)
    {
        return result.Steps.Select(s => new GenerationStep
        {
            StepType = s.StepType,
            Prompt = s.Prompt,
            Response = s.Response,
            ResponseFormat = s.ResponseFormat,
            StructuredResponseJson = s.StructuredResponseJson,
            PromptVersion = s.PromptVersion,
            ValidationStatus = s.ValidationStatus,
            TokenCount = s.TokenCount,
            DurationSeconds = s.DurationSeconds,
            StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt,
            Success = s.Success,
            ErrorMessage = s.ErrorMessage,
            Status = s.Status,
            SkipReason = s.SkipReason
        }).ToList();
    }

    private static string BuildPersistenceErrorDetails(DbUpdateException exception)
    {
        var messages = new List<string>();
        Exception? current = exception;

        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message)) messages.Add(current.Message);

            current = current.InnerException;
        }

        return string.Join(" | ", messages.Distinct());
    }

    private ITestGenerationApproach ResolveGenerationApproach(
        TestMap.Models.Configuration.Testing.Generation.TestGenerationApproach strategy)
    {
        if (_generationApproaches.TryGetValue(strategy, out var approach)) return approach;

        throw new InvalidOperationException($"No generation approach is registered for '{strategy}'.");
    }

    private TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective GetActiveObjective()
    {
        return _activeExperimentConfig?.Objective
               ?? TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective.TestSuiteExpansion;
    }

    internal static bool ShouldRequirePassingExistingTest(
        TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective objective)
    {
        return objective switch
        {
            TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective.TestSuiteExpansion => false,
            _ => true
        };
    }

    private sealed record MemberCodeMetricColumns(
        int MaintainabilityIndex,
        int CyclomaticComplexity,
        int ClassCoupling,
        int DepthOfInheritance,
        int SourceLinesOfCode,
        int ExecutableLinesOfCode);

}
