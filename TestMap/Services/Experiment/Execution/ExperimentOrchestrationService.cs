using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Persistence.Ef.Repositories.RiskScoring;
using TestMap.Services.Configuration;
using TestMap.Services.Rules;
using TestMap.Services.Experiment.Reporting;
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
    private readonly IExperimentResumeService _resumeService;
    private readonly IRuleDecisionRecorder _ruleDecisionRecorder;
    private readonly IExperimentResultsWriter _resultsWriter;
    private readonly ProjectArtifactCleanupService _artifactCleanupService;
    private readonly TestMapDbContext _dbContext;

    private readonly
        IReadOnlyDictionary<TestMap.Models.Configuration.Testing.Generation.TestGenerationApproach,
            ITestGenerationApproach> _generationApproaches;

    private readonly RollbackWorkspaceService _workspace;
    private ExperimentConfig? _activeExperimentConfig;
    private ITestGenerationApproach? _activeGenerationApproach;

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
        IExperimentResumeService resumeService,
        IRuleDecisionRecorder ruleDecisionRecorder,
        IExperimentResultsWriter resultsWriter,
        ProjectArtifactCleanupService artifactCleanupService,
        TestMapDbContext dbContext,
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
        _resumeService = resumeService;
        _ruleDecisionRecorder = ruleDecisionRecorder;
        _resultsWriter = resultsWriter;
        _artifactCleanupService = artifactCleanupService;
        _dbContext = dbContext;
        _generationApproaches = generationApproaches.ToDictionary(x => x.Strategy);
        _workspace = workspace;
    }

    public async Task<ExperimentRun> RunExperimentAsync(
        ExperimentConfig config,
        CancellationToken cancellationToken = default)
    {
        _activeExperimentConfig = config;
        _activeGenerationApproach = ResolveGenerationApproach(config.GenerationApproach);
        await _workspace.EnsureWorkspaceReadyAsync(cancellationToken);
        _artifactCleanupService.CleanupProjectDirectory(false);
        var experimentStopwatch = Stopwatch.StartNew();

        _context.Project.Logger?.Information("=== Starting Experiment Run ===");
        _context.Project.Logger?.Information($"Providers: {string.Join(", ", config.IncludeProviders)}");
        _context.Project.Logger?.Information($"Budget Modes: {string.Join(", ", config.BudgetModes)}");
        _context.Project.Logger?.Information($"Generation approach: {config.GenerationApproach}");
        _context.Project.Logger?.Information($"Executor: {config.Executor}");
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
                                         ?? config.GenerationApproach.ToString(),
            CandidateLimit = config.CandidateLimit,
            ResultsFilePath = ResolveResultsFilePath(config),
            Status = "Running"
        };

        experimentRun.Id = await _experimentRunRepo.InsertAsync(experimentRun, cancellationToken);

        try
        {
            var candidateMethods = await _methodSelection.SelectCandidateMethodsAsync(
                config,
                requirePassingExistingTest: true,
                cancellationToken);
            _context.Project.Logger?.Information($"Selected {candidateMethods.Count} candidate methods");

            foreach (var method in candidateMethods)
            {
                method.ExperimentRunId = experimentRun.Id;
                method.Id = await _candidateMethodRepo.InsertAsync(method, cancellationToken);
                await SaveRiskScoreAsync(method, cancellationToken);
            }

            var providers = GetProvidersToTest(config);
            var matrix = _matrixGenerator.Generate(config, providers);
            await _ruleDecisionRecorder.RecordAsync(
                _context.Project.DbId,
                RuleDecisionScope.ExperimentRun(experimentRun.Id),
                matrix.RuleDecisions,
                experimentRunId: experimentRun.Id,
                cancellationToken: cancellationToken);
            _context.Project.Logger?.Information("Expanded {MatrixCount} experiment matrix item(s).", matrix.Items.Count);

            foreach (var candidateMethod in candidateMethods)
            {
                _context.Project.Logger?.Information($"\n--- Method: {candidateMethod.MethodName} ---");

                var methodContext = await _methodSelection.GetMethodContextAsync(
                    candidateMethod.MemberId,
                    cancellationToken);

                if (methodContext == null)
                {
                    _context.Project.Logger?.Warning($"Could not get context for method {candidateMethod.MethodName}");
                    continue;
                }

                var matrixApproaches = matrix.Items
                    .Select(x => x.Approach)
                    .Distinct()
                    .ToList();
                if (matrixApproaches.Count > 0 &&
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

                foreach (var matrixItem in matrix.Items)
                {
                    var workItem = await EnsureWorkItemAsync(
                        experimentRun,
                        candidateMethod,
                        matrixItem,
                        cancellationToken);
                    var resumeDecision = _resumeService.Evaluate(workItem, config.Resume, DateTime.UtcNow);
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
                        continue;
                    }

                    _context.Project.Logger?.Information(
                        "  Variant: {VariantId}",
                        matrixItem.VariantId);

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
                        foreach (var attempt in attempts)
                        {
                            if (attempt.ParentAttemptNumber.HasValue &&
                                persistedAttemptIdsByAttemptNumber.TryGetValue(
                                    attempt.ParentAttemptNumber.Value,
                                    out var parentAttemptId))
                                attempt.ParentAttemptId = parentAttemptId;

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
            AttemptNumber = attempt.AttemptNumber,
            RepairAttemptNumber = attempt.IsRepairAttempt ? attempt.AttemptNumber : null,
            SourceMemberId = candidateMethod.MemberId,
            SourceMethodName = candidateMethod.MethodName,
            SourceMethodSignature = candidateMethod.Signature,
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
            TotalDurationSeconds = attempt.TotalDurationSeconds,
            PromptVersion = attempt.GenerationSteps.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.PromptVersion))?.PromptVersion ?? string.Empty,
            GenerationAttemptId = attempt.Id,
            TestExecutionId = execution?.Id,
            ResumeStableKey = stableKey
        };
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
            var item = new GenerationExperimentMatrixItem
            {
                VariantId = $"{provider}__{GetActiveGenerationApproach().Strategy}__{budgetMode}__baseline",
                Provider = provider,
                ModelName = ResolveModelName(provider),
                Approach = GetActiveGenerationApproach().Strategy,
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
        var evaluations = await _budgetExecutor.ExecuteAsync(
            new GenerationBudgetExecutionRequest
            {
                BudgetMode = matrixItem.BudgetMode,
                GenerateAsync = (attemptNumber, token) =>
                    ExecuteSingleGenerationAttemptAsync(candidateMethodId, context, matrixItem, attemptNumber, token),
                RepairAsync = (previousAttempt, attemptNumber, token) =>
                    ExecuteSingleRepairAttemptAsync(candidateMethodId, context, matrixItem, previousAttempt, attemptNumber, token),
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
                matrixItem);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _pipeline.GenerateTestAsync(
                CreateGenerationRequest(context, matrixItem),
                cancellationToken);
            attempt.GenerationSteps = MapSteps(result);
            attempt.TotalTokensUsed = result.TotalTokens;

            if (!result.Success || string.IsNullOrEmpty(result.GeneratedTest))
            {
                attempt.ErrorMessage = result.ErrorMessage ?? "Generation failed";
                attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, attempt.ErrorMessage);
                return attempt;
            }

            attempt.TestExecution = await ExecuteAndTestAsync(
                result.GeneratedTest!,
                result.TestMethodName!,
                context,
                matrixItem,
                cancellationToken);

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
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        var attempt = CreateAttempt(
            candidateMethodId,
            matrixItem.Provider,
            matrixItem.BudgetMode,
            attemptNumber,
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

            var repairRequest = CreateRepairRequest(
                context,
                previousAttempt.TestExecution.GeneratedTestCode,
                previousAttempt.TestExecution.ErrorLogs ?? "Test failed",
                previousAttempt.TestExecution.StructuredErrors,
                null,
                matrixItem,
                attemptNumber);

            var result = await _pipeline.RepairTestAsync(repairRequest, cancellationToken);
            attempt.GenerationSteps = MapSteps(result);
            attempt.TotalTokensUsed = result.TotalTokens;

            if (!result.Success || string.IsNullOrEmpty(result.GeneratedTest))
            {
                attempt.ErrorMessage = result.ErrorMessage ?? $"Repair {attemptNumber} failed";
                attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, attempt.ErrorMessage);
                return attempt;
            }

            attempt.TestExecution = await ExecuteAndTestAsync(
                result.GeneratedTest!,
                result.TestMethodName ?? context.Method.MethodName,
                context,
                matrixItem,
                cancellationToken);

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

    private TestRepairRequest CreateRepairRequest(
        CandidateMethodContext context,
        string generatedTest,
        string errorLogs,
        string? structuredErrors,
        string? priorConversationTranscript,
        GenerationExperimentMatrixItem matrixItem,
        int attemptNumber,
        double temperature = 0.0)
    {
        var experimentConfig = _activeExperimentConfig;

        var request = ResolveGenerationApproach(matrixItem.Approach).CreateRepairRequest(new TestRepairApproachContext
        {
            MethodContext = context,
            GeneratedTest = generatedTest,
            ErrorLogs = errorLogs,
            StructuredErrors = structuredErrors,
            PriorConversationTranscript = priorConversationTranscript,
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
            ValidationRuleDecisionJson = _ruleDecisionRecorder.CreateSnapshotJson(validation.RuleDecisions),
            ClassificationRuleDecisionJson = _ruleDecisionRecorder.CreateSnapshotJson(classification.RuleDecisions),
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
            NewRoslynDiagnostics = FormatRoslynDiagnostics(execution.NewRoslynDiagnostics)
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
            ErrorLogs = request.ErrorLogs,
            StructuredErrors = request.StructuredErrors,
            PriorConversationTranscript = request.PriorConversationTranscript,
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
                attempt.RuleDecisionJson,
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
                    step.RuleDecisionJson,
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
                var executionDecisions = ParseRuleDecisions(attempt.TestExecution.ValidationRuleDecisionJson)
                    .Concat(ParseRuleDecisions(attempt.TestExecution.ClassificationRuleDecisionJson))
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
        int attemptNumber, GenerationExperimentMatrixItem? matrixItem = null)
    {
        return new GenerationAttempt
        {
            CandidateMethodId = candidateMethodId,
            Provider = provider,
            ModelName = matrixItem?.ModelName ?? ResolveModelName(provider),
            Objective = _activeExperimentConfig?.Objective
                        ?? TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective.TestSuiteExpansion,
            GenerationApproach = matrixItem?.Approach
                                 ?? _activeGenerationApproach?.Strategy
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
            StartedAt = DateTime.UtcNow
        };
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
            GenerationApproach = _activeGenerationApproach?.Strategy
                                 ?? TestMap.Models.Configuration.Testing.Generation.TestGenerationApproach.MetricsDriven,
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

    private ITestGenerationApproach GetActiveGenerationApproach()
    {
        return _activeGenerationApproach
               ?? throw new InvalidOperationException(
                   "No active generation approach has been configured for the experiment run.");
    }

    private TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective GetActiveObjective()
    {
        return _activeExperimentConfig?.Objective
               ?? TestMap.Models.Configuration.Testing.Generation.TestGenerationObjective.TestSuiteExpansion;
    }

    private sealed record MemberCodeMetricColumns(
        int MaintainabilityIndex,
        int CyclomaticComplexity,
        int ClassCoupling,
        int DepthOfInheritance,
        int SourceLinesOfCode,
        int ExecutableLinesOfCode);

}
