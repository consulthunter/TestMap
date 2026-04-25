using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;
using TestMap.Models.RiskScoring;
using TestMap.Models.Results;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Persistence.Ef.Repositories.RiskScoring;
using TestMap.Services.Configuration;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.StaticAnalysis.Enrichment;
using TestMap.Services.TestGeneration;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.Strategies;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.Workspace;
using BuildTestService = TestMap.Services.TestExecution.BuildTestService;
using BuildTestRunRequest = TestMap.Services.TestExecution.BuildTestRunRequest;
using ExperimentTestExecution = TestMap.Models.Experiment.TestExecution;

namespace TestMap.Services.Experiment.Execution;

public class ExperimentOrchestrationService : IExperimentOrchestrationService
{
    private readonly ProjectContext _context;
    private readonly TestMap.Models.Configuration.TestMapConfig _config;
    private readonly IMethodSelectionService _methodSelection;
    private readonly ITestGenerationPipelineService _pipeline;
    private readonly BuildTestService _buildTest;
    private readonly ExperimentRunRepository _experimentRunRepo;
    private readonly CandidateMethodRepository _candidateMethodRepo;
    private readonly GenerationAttemptRepository _attemptRepo;
    private readonly GenerationStepRepository _stepRepo;
    private readonly TestExecutionRepository _executionRepo;
    private readonly CandidateMethodRiskScoreRepository _riskScoreRepo;
    private readonly IAnalyzeProjectService _analyzeProjectService;
    private readonly ICodeMetricsService _codeMetricsService;
    private readonly ITestSmellService _testSmellService;
    private readonly TestMapDbContext _dbContext;

    private readonly
        IReadOnlyDictionary<TestMap.Models.Configuration.Testing.Generation.TestGenerationApproach,
            ITestGenerationApproach> _generationApproaches;

    private readonly
        IReadOnlyDictionary<TestMap.Models.Configuration.Testing.Generation.TestActionExecutorMode, ITestActionExecutor>
        _actionExecutors;

    private readonly RollbackWorkspaceService _workspace;
    private ExperimentConfig? _activeExperimentConfig;
    private ITestGenerationApproach? _activeGenerationApproach;
    private ITestActionExecutor? _activeActionExecutor;

    public ExperimentOrchestrationService(
        ProjectContext context,
        TestMap.Models.Configuration.TestMapConfig config,
        IMethodSelectionService methodSelection,
        ITestGenerationPipelineService pipeline,
        BuildTestService buildTest,
        ExperimentRunRepository experimentRunRepo,
        CandidateMethodRepository candidateMethodRepo,
        GenerationAttemptRepository attemptRepo,
        GenerationStepRepository stepRepo,
        TestExecutionRepository executionRepo,
        CandidateMethodRiskScoreRepository riskScoreRepo,
        IAnalyzeProjectService analyzeProjectService,
        ICodeMetricsService codeMetricsService,
        ITestSmellService testSmellService,
        TestMapDbContext dbContext,
        IEnumerable<ITestGenerationApproach> generationApproaches,
        IEnumerable<ITestActionExecutor> actionExecutors,
        RollbackWorkspaceService workspace)
    {
        _context = context;
        _config = config;
        _methodSelection = methodSelection;
        _pipeline = pipeline;
        _buildTest = buildTest;
        _experimentRunRepo = experimentRunRepo;
        _candidateMethodRepo = candidateMethodRepo;
        _attemptRepo = attemptRepo;
        _stepRepo = stepRepo;
        _executionRepo = executionRepo;
        _riskScoreRepo = riskScoreRepo;
        _analyzeProjectService = analyzeProjectService;
        _codeMetricsService = codeMetricsService;
        _testSmellService = testSmellService;
        _dbContext = dbContext;
        _generationApproaches = generationApproaches.ToDictionary(x => x.Strategy);
        _actionExecutors = actionExecutors.ToDictionary(x => x.Mode);
        _workspace = workspace;
    }

    public async Task<ExperimentRun> RunExperimentAsync(
        ExperimentConfig config,
        CancellationToken cancellationToken = default)
    {
        _activeExperimentConfig = config;
        _activeGenerationApproach = ResolveGenerationApproach(config.GenerationApproach);
        _activeActionExecutor = ResolveActionExecutor(config.Executor);
        await _workspace.EnsureWorkspaceReadyAsync(cancellationToken);
        var experimentStopwatch = Stopwatch.StartNew();

        _context.Project.Logger?.Information("=== Starting Experiment Run ===");
        _context.Project.Logger?.Information($"Providers: {string.Join(", ", config.IncludeProviders)}");
        _context.Project.Logger?.Information($"Strategies: {string.Join(", ", config.Strategies)}");
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
            CandidateLimit = config.CandidateLimit,
            Status = "Running"
        };

        experimentRun.Id = await _experimentRunRepo.InsertAsync(experimentRun, cancellationToken);

        try
        {
            var candidateMethods = await _methodSelection.SelectCandidateMethodsAsync(config, cancellationToken);
            _context.Project.Logger?.Information($"Selected {candidateMethods.Count} candidate methods");

            foreach (var method in candidateMethods)
            {
                method.ExperimentRunId = experimentRun.Id;
                method.Id = await _candidateMethodRepo.InsertAsync(method, cancellationToken);
                await SaveRiskScoreAsync(method, cancellationToken);
            }

            var providers = GetProvidersToTest(config);

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

                if (GetActiveGenerationApproach().ShouldSkipGeneration(methodContext))
                {
                    _context.Project.Logger?.Information(
                        "Skipping method {MethodName} because the active generation approach marked it as skip.",
                        candidateMethod.MethodName);
                    continue;
                }

                candidateMethod.ExistingTestMemberId = methodContext.Method.ExistingTestMemberId;
                candidateMethod.ExistingTestMethodName = methodContext.Method.ExistingTestMethodName;
                await _candidateMethodRepo.UpdateAsync(candidateMethod, cancellationToken);

                foreach (var provider in providers)
                {
                    _context.Project.Logger?.Information($"\n  Provider: {provider}");

                    foreach (var strategy in config.Strategies)
                    {
                        _context.Project.Logger?.Information($"    Strategy: {strategy}");

                        try
                        {
                            var attempts = await ExecuteGenerationAttemptAsync(
                                candidateMethod,
                                methodContext,
                                provider,
                                strategy,
                                cancellationToken);

                            foreach (var attempt in attempts)
                                await SaveGenerationAttemptAsync(attempt, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _context.Project.Logger?.Error(
                                ex,
                                "Failed to execute {Provider}/{Strategy} for {MethodName}",
                                provider,
                                strategy,
                                candidateMethod.MethodName);
                        }
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

    public async Task<IReadOnlyList<GenerationAttempt>> ExecuteGenerationAttemptAsync(
        CandidateMethod candidateMethod,
        CandidateMethodContext context,
        AiProvider provider,
        GenerationStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return strategy switch
            {
                GenerationStrategy.Pass1 =>
                    [await ExecutePass1Async(candidateMethod.Id, context, provider, cancellationToken)],
                GenerationStrategy.Pass5 => await ExecutePass5Async(candidateMethod.Id, context, provider,
                    cancellationToken),
                GenerationStrategy.Repair5 => await ExecuteRepair5Async(candidateMethod.Id, context, provider,
                    cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported strategy: {strategy}")
            };
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error(ex, "Generation attempt failed.");
            return [CreateFailedAttempt(candidateMethod.Id, provider, strategy, 1, ex.Message)];
        }
    }

    private async Task<GenerationAttempt> ExecutePass1Async(
        int candidateMethodId,
        CandidateMethodContext context,
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        var attempt = CreateAttempt(candidateMethodId, provider, GenerationStrategy.Pass1, 1);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result =
                await _pipeline.GenerateTestAsync(CreateGenerationRequest(context, provider), cancellationToken);
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

    private async Task<IReadOnlyList<GenerationAttempt>> ExecutePass5Async(
        int candidateMethodId,
        CandidateMethodContext context,
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        var attempts = new List<GenerationAttempt>();

        for (var i = 1; i <= 5; i++)
        {
            _context.Project.Logger?.Information($"      Attempt {i}/5");
            var attempt = CreateAttempt(candidateMethodId, provider, GenerationStrategy.Pass5, i);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await _pipeline.GenerateTestAsync(
                    CreateGenerationRequest(context, provider, 0.7),
                    cancellationToken);

                attempt.GenerationSteps = MapSteps(result);
                attempt.TotalTokensUsed = result.TotalTokens;

                if (!result.Success || string.IsNullOrEmpty(result.GeneratedTest))
                {
                    attempt.ErrorMessage = result.ErrorMessage ?? $"Generation {i} failed";
                    attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, attempt.ErrorMessage);
                }
                else
                {
                    attempt.TestExecution = await ExecuteAndTestAsync(
                        result.GeneratedTest!,
                        result.TestMethodName!,
                        context,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                attempt.ErrorMessage = ex.Message;
                attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, ex.Message);
                _context.Project.Logger?.Error(ex, "Pass5 attempt {AttemptNumber} failed.", i);
            }
            finally
            {
                stopwatch.Stop();
                attempt.CompletedAt = DateTime.UtcNow;
                attempt.TotalDurationSeconds = stopwatch.Elapsed.TotalSeconds;
                attempts.Add(attempt);
                await _workspace.RollbackChangesAsync(cancellationToken);
            }
        }

        var best = attempts
            .Where(x => x.TestExecution != null)
            .OrderByDescending(x => x.TestExecution!.TestPassed)
            .ThenByDescending(x => x.TestExecution!.CoverageImprovement)
            .FirstOrDefault();

        if (best?.TestExecution != null)
            _context.Project.Logger?.Information(
                $"      Best: Passed={best.TestExecution.TestPassed}, Coverage={best.TestExecution.CoverageImprovement:P}");

        return attempts;
    }

    private async Task<IReadOnlyList<GenerationAttempt>> ExecuteRepair5Async(
        int candidateMethodId,
        CandidateMethodContext context,
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        string? currentTest = null;
        string? currentTestMethodName = null;
        string? currentConversationTranscript = null;
        ExperimentTestExecution? lastExecution = null;
        var attempts = new List<GenerationAttempt>();

        for (var i = 1; i <= 5; i++)
        {
            _context.Project.Logger?.Information($"      Attempt {i}/5");
            var attempt = CreateAttempt(candidateMethodId, provider, GenerationStrategy.Repair5, i);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                TestGenerationResult result;

                if (i == 1)
                {
                    result = await _pipeline.GenerateTestAsync(CreateGenerationRequest(context, provider),
                        cancellationToken);
                }
                else
                {
                    if (currentTest == null || lastExecution == null)
                    {
                        attempt.ErrorMessage = "No previous attempt available for repair.";
                        attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, attempt.ErrorMessage);
                        attempts.Add(attempt);
                        break;
                    }

                    var repairRequest = CreateRepairRequest(
                        context,
                        currentTest,
                        lastExecution.ErrorLogs ?? "Test failed",
                        lastExecution.StructuredErrors,
                        currentConversationTranscript,
                        provider,
                        i);

                    result = await _pipeline.RepairTestAsync(repairRequest, cancellationToken);
                }

                attempt.GenerationSteps = MapSteps(result);
                attempt.TotalTokensUsed = result.TotalTokens;

                if (!result.Success || string.IsNullOrEmpty(result.GeneratedTest))
                {
                    attempt.ErrorMessage = result.ErrorMessage ?? $"Repair {i} failed";
                    attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, attempt.ErrorMessage);
                    attempts.Add(attempt);
                    break;
                }

                currentTest = result.GeneratedTest;
                currentTestMethodName = result.TestMethodName;
                currentConversationTranscript = result.ConversationTranscript;
                lastExecution = await ExecuteAndTestAsync(
                    currentTest,
                    currentTestMethodName ?? context.Method.MethodName,
                    context,
                    cancellationToken);

                attempt.TestExecution = lastExecution;

                if (lastExecution.TestPassed && lastExecution.CoverageImprovement > 0)
                {
                    _context.Project.Logger?.Information($"      Success on attempt {i}");
                    attempts.Add(attempt);
                    break;
                }

                _context.Project.Logger?.Warning(
                    $"      Attempt {i} failed: Passed={lastExecution.TestPassed}, Coverage={lastExecution.CoverageImprovement:P}");
            }
            catch (Exception ex)
            {
                attempt.ErrorMessage = ex.Message;
                attempt.TestExecution = CreateFailedExecution(TestFailureKind.Generation, ex.Message);
                _context.Project.Logger?.Error(ex, "Repair5 attempt {AttemptNumber} failed.", i);
            }
            finally
            {
                stopwatch.Stop();
                attempt.CompletedAt = DateTime.UtcNow;
                attempt.TotalDurationSeconds = stopwatch.Elapsed.TotalSeconds;
                if (!attempts.Contains(attempt))
                    attempts.Add(attempt);
                await _workspace.RollbackChangesAsync(cancellationToken);
            }
        }

        return attempts;
    }

    private TestGenerationRequest CreateGenerationRequest(
        CandidateMethodContext context,
        AiProvider provider,
        double temperature = 0.0)
    {
        var experimentConfig = _activeExperimentConfig;

        return GetActiveGenerationApproach().CreateGenerationRequest(new TestGenerationApproachContext
        {
            MethodContext = context,
            Provider = provider,
            Temperature = temperature,
            StepErrorRetries = Math.Max(0, experimentConfig?.StepErrorRetries ?? 0),
            StepRetryDelayMs = Math.Max(0, experimentConfig?.StepRetryDelayMs ?? 1000),
            EnableHistoryChaining = _config.TestingConfig.GenerationConfig.EnableHistoryChaining
        });
    }

    private TestRepairRequest CreateRepairRequest(
        CandidateMethodContext context,
        string generatedTest,
        string errorLogs,
        string? structuredErrors,
        string? priorConversationTranscript,
        AiProvider provider,
        int attemptNumber,
        double temperature = 0.0)
    {
        var experimentConfig = _activeExperimentConfig;

        return GetActiveGenerationApproach().CreateRepairRequest(new TestRepairApproachContext
        {
            MethodContext = context,
            GeneratedTest = generatedTest,
            ErrorLogs = errorLogs,
            StructuredErrors = structuredErrors,
            PriorConversationTranscript = priorConversationTranscript,
            Provider = provider,
            Temperature = temperature,
            AttemptNumber = attemptNumber,
            StepErrorRetries = Math.Max(0, experimentConfig?.StepErrorRetries ?? 0),
            StepRetryDelayMs = Math.Max(0, experimentConfig?.StepRetryDelayMs ?? 1000),
            EnableHistoryChaining = _config.TestingConfig.GenerationConfig.EnableHistoryChaining
        });
    }

    private async Task<ExperimentTestExecution> ExecuteAndTestAsync(
        string generatedTest,
        string testMethodName,
        CandidateMethodContext context,
        CancellationToken cancellationToken)
    {
        var execution = new ExperimentTestExecution
        {
            GeneratedTestCode = generatedTest,
            GeneratedTestMethodName = testMethodName,
            ExecutedAt = DateTime.UtcNow
        };

        try
        {
            var actionResult = await GetActiveActionExecutor().ExecuteAsync(
                context,
                generatedTest,
                testMethodName,
                cancellationToken);

            if (!actionResult.Success)
                throw new InvalidOperationException(
                    actionResult.ErrorMessage ??
                    $"Failed to apply generated test into {context.TestFilePath} for class {context.TestClassName}.");

            var validationResult = await _buildTest.ValidateBuildAsync(
                context.TestProjectPath,
                cancellationToken);

            if (!validationResult.IsSuccess)
            {
                execution.CompilationSuccess = false;
                execution.TestPassed = false;
                execution.FailureKind = TestFailureKind.Compilation;
                execution.CompilationErrors = validationResult.LogText;
                execution.StructuredErrors = validationResult.StructuredErrors;
                execution.ErrorLogs = validationResult.LogText;
                execution.FailureStage = "build";
                execution.FailureCategory = "docker_compilation_validation_failed";
                execution.FailureSummary = "Docker build validation failed before test execution.";
                return execution;
            }

            await RefreshProjectMetadataAsync(context.TestProjectPath);

            var buildResult = await _buildTest.BuildTestAsync(
                BuildTestRunRequest.CreateIteration(
                    context.TestProjectPath,
                    context.TargetBuildFramework,
                    context.Method.MethodName,
                    context.SourceProjectPath));

            execution.CompilationSuccess = DidCompilationSucceed(buildResult);
            execution.TestPassed = execution.CompilationSuccess &&
                                   buildResult.Results.Count > 0 &&
                                   buildResult.Results.All(r => r.Outcome == "Passed");
            execution.CoverageAfter = buildResult is GeneratedTestRunModel generatedRun
                ? generatedRun.MethodCoverage
                : buildResult.Coverage / 100.0;
            execution.CoverageImprovement = execution.CoverageAfter - context.Method.BaselineCoverage;
            execution.BaselineMutationScore = await GetLatestBaselineMutationScoreAsync(cancellationToken);
            execution.MutationScoreAfter = buildResult.MutationScore;
            execution.MutationScoreImprovement =
                execution.MutationScoreAfter.HasValue && execution.BaselineMutationScore.HasValue
                    ? execution.MutationScoreAfter.Value - execution.BaselineMutationScore.Value
                    : null;
            execution.Classification = execution.TestPassed
                ? execution.CoverageImprovement > 0 ? TestClassification.Approved : TestClassification.Benign
                : execution.CoverageImprovement > 0
                    ? TestClassification.Candidate
                    : TestClassification.Failed;

            if (!execution.CompilationSuccess)
            {
                execution.TestPassed = false;
                execution.FailureKind = buildResult.FailureAnalysis != null
                    ? MapFailureKind(buildResult.FailureAnalysis)
                    : TestFailureKind.Compilation;
                execution.CompilationErrors = buildResult.FailureAnalysis?.Evidence;
                execution.ErrorLogs = await ReadExecutionLogAsync(buildResult.LogPath, cancellationToken)
                                      ?? buildResult.FailureAnalysis?.Evidence;
                execution.FailureStage = buildResult.FailureAnalysis?.Stage ?? "build";
                execution.FailureCategory = buildResult.FailureAnalysis?.Category ?? "build_failed";
                execution.FailureSummary =
                    buildResult.FailureAnalysis?.Summary ?? "Build failed before test execution.";
            }
            else if (buildResult.FailureAnalysis != null && buildResult.Results.Count == 0)
            {
                execution.TestPassed = false;
                execution.FailureKind = MapFailureKind(buildResult.FailureAnalysis);
                execution.RuntimeErrors = buildResult.FailureAnalysis.Evidence;
                execution.ErrorLogs = await ReadExecutionLogAsync(buildResult.LogPath, cancellationToken)
                                      ?? buildResult.FailureAnalysis.Evidence;
                execution.FailureStage = buildResult.FailureAnalysis.Stage;
                execution.FailureCategory = buildResult.FailureAnalysis.Category;
                execution.FailureSummary = buildResult.FailureAnalysis.Summary;
            }
            else if (!execution.TestPassed)
            {
                execution.FailureKind = TestFailureKind.Runtime;
                execution.RuntimeErrors = string.Join(
                    "\n",
                    buildResult.Results.Where(r => r.Outcome != "Passed").Select(r => r.ErrorMessage));
                execution.AssertionErrors = execution.RuntimeErrors;
                execution.ErrorLogs = execution.RuntimeErrors;
            }
            else
            {
                execution.FailureKind = TestFailureKind.None;
            }

            _context.Project.Logger?.Information(
                $"      Compiled: {execution.CompilationSuccess}, Passed: {execution.TestPassed}, Coverage: {execution.CoverageAfter:P}");
        }
        catch (Exception ex)
        {
            execution.CompilationSuccess = false;
            execution.TestPassed = false;
            execution.FailureKind = TestFailureKind.Infrastructure;
            execution.RuntimeErrors = ex.Message;
            execution.ErrorLogs = ex.ToString();
            execution.FailureStage = "execution";
            execution.FailureCategory = "unexpected_execution_exception";
            execution.FailureSummary = "An unexpected exception occurred while executing the generated test.";
            _context.Project.Logger?.Error(ex, "      Execution failed.");
        }

        return execution;
    }

    private async Task RefreshProjectMetadataAsync(string testProjectPath)
    {
        var analysisProject = _context.Project.Projects.FirstOrDefault(x =>
            string.Equals(Path.GetFullPath(x.FilePath), Path.GetFullPath(testProjectPath),
                StringComparison.OrdinalIgnoreCase));

        if (analysisProject == null)
        {
            _context.Project.Logger?.Warning(
                "Skipping metadata refresh because test project was not found in loaded project metadata: {TestProjectPath}",
                testProjectPath);
            return;
        }

        await _analyzeProjectService.AnalyzeProjectAsync(analysisProject);
        await _codeMetricsService.CollectCodeMetricsAsync(analysisProject);

        if (_context.Project.DbId != 0) await _testSmellService.CollectAsync(testProjectPath, _context.Project.DbId);
    }

    private async Task<double?> GetLatestBaselineMutationScoreAsync(CancellationToken cancellationToken)
    {
        if (_context.Project.DbId == 0) return null;

        return await _dbContext.TestRuns
            .Where(x => x.ProjectId == _context.Project.DbId)
            .Where(x => x.RunId.StartsWith("baseline_"))
            .Where(x => x.MutationScore != null)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.MutationScore)
            .FirstOrDefaultAsync(cancellationToken);
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

    private async Task SaveGenerationAttemptAsync(GenerationAttempt attempt, CancellationToken cancellationToken)
    {
        try
        {
            attempt.Id = await _attemptRepo.InsertAsync(attempt, cancellationToken);

            foreach (var step in attempt.GenerationSteps)
            {
                step.GenerationAttemptId = attempt.Id;
                await _stepRepo.InsertAsync(step, cancellationToken);
            }

            if (attempt.TestExecution != null)
            {
                attempt.TestExecution.GenerationAttemptId = attempt.Id;
                await _executionRepo.InsertAsync(attempt.TestExecution, cancellationToken);
            }
        }
        catch (DbUpdateException ex)
        {
            var detail = BuildPersistenceErrorDetails(ex);
            _context.Project.Logger?.Error(
                "Failed to persist generation attempt {CandidateMethodId}/{Provider}/{Strategy}/{AttemptNumber}: {Details}",
                attempt.CandidateMethodId,
                attempt.Provider,
                attempt.Strategy,
                attempt.AttemptNumber,
                detail);
            throw new InvalidOperationException(
                $"Failed to persist generation attempt {attempt.CandidateMethodId}/{attempt.Provider}/{attempt.Strategy}/{attempt.AttemptNumber}: {detail}",
                ex);
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

    private GenerationAttempt CreateAttempt(int candidateMethodId, AiProvider provider, GenerationStrategy strategy,
        int attemptNumber)
    {
        return new GenerationAttempt
        {
            CandidateMethodId = candidateMethodId,
            Provider = provider,
            Strategy = strategy,
            AttemptNumber = attemptNumber,
            StartedAt = DateTime.UtcNow
        };
    }

    private GenerationAttempt CreateFailedAttempt(int candidateMethodId, AiProvider provider,
        GenerationStrategy strategy, int attemptNumber, string errorMessage)
    {
        return new GenerationAttempt
        {
            CandidateMethodId = candidateMethodId,
            Provider = provider,
            Strategy = strategy,
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
            ErrorMessage = s.ErrorMessage
        }).ToList();
    }

    private static TestFailureKind MapFailureKind(FailureAnalysisModel failureAnalysis)
    {
        return failureAnalysis.Stage switch
        {
            "restore" or "build" => TestFailureKind.Compilation,
            "test" or "coverage" => TestFailureKind.Runtime,
            "infrastructure" => TestFailureKind.Infrastructure,
            _ => TestFailureKind.Unknown
        };
    }

    private static bool DidCompilationSucceed(TestRunModel buildResult)
    {
        if (buildResult.Results.Count == 0 && buildResult.Coverage == 0) return false;

        return buildResult.FailureAnalysis?.Stage switch
        {
            "restore" or "build" or "infrastructure" or "unknown" => false,
            _ => true
        };
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

    private static async Task<string?> ReadExecutionLogAsync(string? logPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath)) return null;

        return await File.ReadAllTextAsync(logPath, cancellationToken);
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

    private ITestActionExecutor ResolveActionExecutor(
        TestMap.Models.Configuration.Testing.Generation.TestActionExecutorMode mode)
    {
        if (_actionExecutors.TryGetValue(mode, out var executor)) return executor;

        throw new InvalidOperationException($"No test action executor is registered for '{mode}'.");
    }

    private ITestActionExecutor GetActiveActionExecutor()
    {
        return _activeActionExecutor
               ?? throw new InvalidOperationException(
                   "No active test action executor has been configured for the experiment run.");
    }
}
