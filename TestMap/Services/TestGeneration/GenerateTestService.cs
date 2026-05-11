using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;
using TestMap.Models.Results;
using TestMap.Services.Configuration;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration.Acceptance;
using TestMap.Services.TestGeneration.Bootstrap;
using TestMap.Services.TestGeneration.Evidence;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.Strategies;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.Validation;
using TestMap.Services.TestGeneration.Workspace;

namespace TestMap.Services.TestGeneration;

/// <summary>
/// Service for regular test generation using the shared decomposed pipeline.
/// Uses the configured AI provider to generate tests for low-coverage methods.
/// </summary>
public class GenerateTestService : IGenerateTestService
{
    private readonly ProjectContext _context;
    private readonly TestMapConfig _config;
    private readonly BuildTestService _buildTestService;
    private readonly ITestGenerationPipelineService _pipelineService;
    private readonly ITestBootstrapService _testBootstrapService;
    private readonly IMethodSelectionService _methodSelectionService;
    private readonly IAnalyzeProjectService _analyzeProjectService;

    private readonly
        IReadOnlyDictionary<Models.Configuration.Testing.Generation.TestGenerationApproach, ITestGenerationApproach>
        _generationApproaches;

    private readonly IGeneratedTestExecutionService _generatedTestExecutionService;
    private readonly IGenerationValidationService _generationValidationService;
    private readonly IGenerationAcceptanceService _generationAcceptanceService;
    private readonly BranchWorkspaceService _workspace;

    public GenerateTestService(
        ProjectContext context,
        TestMapConfig config,
        BuildTestService buildTestService,
        ITestGenerationPipelineService pipelineService,
        ITestBootstrapService testBootstrapService,
        IMethodSelectionService methodSelectionService,
        IAnalyzeProjectService analyzeProjectService,
        IEnumerable<ITestGenerationApproach> generationApproaches,
        IGeneratedTestExecutionService generatedTestExecutionService,
        IGenerationValidationService generationValidationService,
        IGenerationAcceptanceService generationAcceptanceService,
        BranchWorkspaceService workspace)
    {
        _context = context;
        _config = config;
        _buildTestService = buildTestService;
        _pipelineService = pipelineService;
        _testBootstrapService = testBootstrapService;
        _methodSelectionService = methodSelectionService;
        _analyzeProjectService = analyzeProjectService;
        _generationApproaches = generationApproaches.ToDictionary(x => x.Strategy);
        _generatedTestExecutionService = generatedTestExecutionService;
        _generationValidationService = generationValidationService;
        _generationAcceptanceService = generationAcceptanceService;
        _workspace = workspace;
    }

    public async Task GenerateTestAsync()
    {
        _context.Project.Logger?.Information("Starting test generation using decomposed pipeline...");

        var generationConfig = _config.TestingConfig.GenerationConfig;
        var provider = generationConfig.Provider;
        var budgetMode = generationConfig.BudgetMode;
        var temperature = generationConfig.Temperature;
        var stepErrorRetries = generationConfig.StepErrorRetries;
        var stepRetryDelayMs = generationConfig.StepRetryDelayMs;
        var generationApproach = ResolveGenerationApproach(generationConfig.Strategy);
        var actionExecutorMode = GenerationObjectivePolicy.ResolveExecutor(generationConfig.Objective);

        ValidateGenerationConfiguration(
            provider,
            generationConfig.Strategy,
            generationConfig.Executor,
            budgetMode,
            temperature,
            stepErrorRetries,
            stepRetryDelayMs);
        await EnsureBootstrapInfrastructureAsync();
        await _workspace.EnsureWorkspaceReadyAsync();

        var selectionConfig = CreateSelectionConfiguration();
        var candidateMethods = await _methodSelectionService.SelectCandidateMethodsAsync(selectionConfig);

        if (candidateMethods.Count == 0)
        {
            _context.Project.Logger?.Warning("No candidate methods were found for test generation.");
            return;
        }

        var successCount = 0;

        foreach (var candidateMethod in candidateMethods)
        {
            var methodContext = await _methodSelectionService.GetMethodContextAsync(candidateMethod.MemberId);
            if (methodContext == null)
            {
                _context.Project.Logger?.Warning(
                    "Skipping method {MethodName} because context could not be resolved.",
                    candidateMethod.MethodName);
                continue;
            }

            if (generationApproach.ShouldSkipGeneration(methodContext))
            {
                _context.Project.Logger?.Information(
                    "Skipping method {MethodName} because the active generation strategy marked it as skip.",
                    candidateMethod.MethodName);
                continue;
            }

            var candidateInfo = ToCandidateMethodInfo(methodContext);

            var succeeded = await GenerateTestForMethodAsync(
                methodContext,
                candidateInfo,
                provider,
                budgetMode,
                temperature,
                stepErrorRetries,
                stepRetryDelayMs,
                generationApproach,
                actionExecutorMode);
            if (succeeded) successCount++;
        }

        _context.Project.Logger?.Information(
            "Test generation complete. Generated {SuccessCount} successful tests from {CandidateCount} candidates.",
            successCount,
            candidateMethods.Count);
    }

    /// <summary>
    /// Generates a test for a specific method using the decomposed pipeline with retry logic.
    /// </summary>
    private async Task<bool> GenerateTestForMethodAsync(
        CandidateMethodContext methodContext,
        CandidateMethodInfo method,
        AiProvider provider,
        Models.Configuration.Testing.Generation.GenerationBudgetMode budgetMode,
        double temperature,
        int stepErrorRetries,
        int stepRetryDelayMs,
        ITestGenerationApproach generationApproach,
        Models.Configuration.Testing.Generation.TestActionExecutorMode actionExecutorMode,
        CancellationToken cancellationToken = default)
    {
        string? candidateTest = null;
        string? resultHistory = null;
        var generationConfig = _config.TestingConfig.GenerationConfig;
        var attempts = GenerationBudgetPlanner.Plan(budgetMode);

        foreach (var attempt in attempts)
        {
            _context.Project.Logger?.Information(
                "[Attempt {Attempt}/{AttemptCount}] Generating test for {MethodName} using {BudgetMode}",
                attempt.AttemptNumber,
                attempts.Count,
                budgetMode,
                method.MethodName);

            TestGenerationResult result;

            if (!attempt.IsRepair)
            {
                var request = generationApproach.CreateGenerationRequest(new TestGenerationApproachContext
                {
                    MethodContext = methodContext,
                    Provider = provider,
                    Temperature = temperature,
                    StepErrorRetries = stepErrorRetries,
                    StepRetryDelayMs = stepRetryDelayMs
                });

                result = await _pipelineService.GenerateTestAsync(
                    ApplyGenerationConfiguration(request, generationConfig),
                    cancellationToken);
            }
            else
            {
                var repairRequest = generationApproach.CreateRepairRequest(new TestRepairApproachContext
                {
                    MethodContext = methodContext,
                    GeneratedTest = candidateTest ?? string.Empty,
                    ErrorLogs = await ReadLatestErrorLogsAsync(cancellationToken),
                    StructuredErrors = await ReadLatestStructuredErrorsAsync(cancellationToken),
                    PriorConversationTranscript = resultHistory,
                    Provider = provider,
                    Temperature = temperature,
                    AttemptNumber = attempt.AttemptNumber,
                    StepErrorRetries = stepErrorRetries,
                    StepRetryDelayMs = stepRetryDelayMs
                });

                result = await _pipelineService.RepairTestAsync(
                    ApplyGenerationConfiguration(repairRequest, generationConfig),
                    cancellationToken);
            }

            if (!result.Success || string.IsNullOrWhiteSpace(result.GeneratedTest))
            {
                _context.Project.Logger?.Warning("Generation failed: {ErrorMessage}", result.ErrorMessage);
                await _workspace.RollbackChangesAsync(cancellationToken);
                continue;
            }

            candidateTest = result.GeneratedTest;
            resultHistory = result.ConversationTranscript;
            var testMethodName = result.TestMethodName ?? Utilities.Utilities.ExtractTestMethodName(candidateTest);

            if (string.IsNullOrWhiteSpace(testMethodName))
            {
                _context.Project.Logger?.Warning(
                    "Generated test for {MethodName} did not include a detectable test method name.",
                    method.MethodName);
                await _workspace.RollbackChangesAsync(cancellationToken);
                continue;
            }

            _context.Project.Logger?.Information(
                "Generated test in {DurationSeconds:F2}s using {TotalTokens} tokens",
                result.TotalDurationSeconds,
                result.TotalTokens);

            var executionResult = await _generatedTestExecutionService.ExecuteAsync(
                methodContext,
                candidateTest,
                testMethodName,
                actionExecutorMode,
                cancellationToken);

            if (!executionResult.ApplicationSucceeded || !File.Exists(method.TestFilePath))
            {
                _context.Project.Logger?.Warning(
                    "Generated test for {MethodName} could not be applied or verified in {TestFilePath}. Reason={Reason}",
                    method.MethodName,
                    method.TestFilePath,
                    executionResult.FailureSummary ?? executionResult.ErrorLogs ?? "Application failed.");
                await _workspace.RollbackChangesAsync(cancellationToken);
                continue;
            }

            var evidence = CreateValidationEvidence(methodContext, generationConfig);
            var validation = _generationValidationService.Validate(executionResult, methodContext, evidence);
            var acceptance = _generationAcceptanceService.Evaluate(
                validation,
                _config.TestingConfig.GenerationConfig.Acceptance);

            if (acceptance.Accepted)
            {
                _context.Project.Logger?.Information(
                    "Generated test accepted. Reason={Reason}, BaselineCoverage={BaselineCoverage:P}, NewCoverage={NewCoverage:P}",
                    acceptance.Reason,
                    method.BaselineCoverage,
                    executionResult.CoverageAfter);
                await _workspace.PersistAcceptedChangesAsync(
                    $"Add generated test for {method.MethodName}",
                    cancellationToken);
                return true;
            }

            _context.Project.Logger?.Warning(
                "Generated test for {MethodName} did not meet criteria. Reason={Reason}, Compiled={Compiled}, Passed={Passed}, CoverageImproved={CoverageImproved}, Baseline={BaselineCoverage:P}, Current={NewCoverage:P}",
                method.MethodName,
                acceptance.Reason,
                validation.CompilationSucceeded,
                validation.AllTestsPassed,
                validation.CoverageImproved,
                method.BaselineCoverage,
                executionResult.CoverageAfter);

            await _workspace.RollbackChangesAsync(cancellationToken);
        }

        _context.Project.Logger?.Error(
            "Failed to generate a valid test for {MethodName} after {AttemptCount} attempts",
            method.MethodName,
            attempts.Count);
        return false;
    }

    private ExperimentConfig CreateSelectionConfiguration()
    {
        var generationConfig = _config.TestingConfig.GenerationConfig;
        var experimentConfig = _config.ExperimentConfig;
        var candidateLimit = generationConfig.TargetSelection.CandidateLimit;
        var bootstrapLimit = generationConfig.Bootstrap.InitialCandidateLimit;

        if (_context.TestBootstrapState != null && bootstrapLimit > 0)
            candidateLimit = candidateLimit > 0
                ? Math.Min(candidateLimit, bootstrapLimit)
                : bootstrapLimit;

        return new ExperimentConfig
        {
            CandidateLimit = candidateLimit,
            MinCoverageThreshold = experimentConfig.MinCoverageThreshold,
            MaxCoverageThreshold = experimentConfig.MaxCoverageThreshold,
            CandidateSelectionStrategy = generationConfig.TargetSelection.Strategy,
            GenerationApproach = generationConfig.Strategy
        };
    }

    private ITestGenerationApproach ResolveGenerationApproach(
        Models.Configuration.Testing.Generation.TestGenerationApproach strategy)
    {
        if (_generationApproaches.TryGetValue(strategy, out var approach)) return approach;

        throw new InvalidOperationException($"No generation approach is registered for '{strategy}'.");
    }

    private void ValidateGenerationConfiguration(
        AiProvider provider,
        Models.Configuration.Testing.Generation.TestGenerationApproach approach,
        Models.Configuration.Testing.Generation.TestActionExecutorMode executor,
        Models.Configuration.Testing.Generation.GenerationBudgetMode budgetMode,
        double temperature,
        int stepErrorRetries,
        int stepRetryDelayMs)
    {
        ExperimentConfigurationValidator.ValidateGenerationConfig(_config.TestingConfig.GenerationConfig.Objective, approach, executor);

        _ = GenerationBudgetPlanner.Plan(budgetMode);

        if (temperature is < 0.0 or > 2.0)
            throw new InvalidOperationException(
                "TestingConfig.GenerationConfig.Temperature must be between 0.0 and 2.0.");

        if (stepErrorRetries < 0)
            throw new InvalidOperationException(
                "TestingConfig.GenerationConfig.StepErrorRetries must be greater than or equal to 0.");

        if (stepRetryDelayMs < 0)
            throw new InvalidOperationException(
                "TestingConfig.GenerationConfig.StepRetryDelayMs must be greater than or equal to 0.");

        var providerConfig = _config.AiProviderConfig.GetProviderConfig(provider);
        if (providerConfig == null) throw new InvalidOperationException($"Provider '{provider}' is not configured.");

        var validationError = AiProviderConfigurationRules.GetValidationError(providerConfig);
        if (!string.IsNullOrWhiteSpace(validationError)) throw new InvalidOperationException(validationError);
    }

    private async Task EnsureBootstrapInfrastructureAsync()
    {
        if (!_config.TestingConfig.GenerationConfig.Bootstrap.Enabled) return;

        var plan = await _testBootstrapService.EnsureBootstrapAsync();
        if (plan.Detection.ShouldBootstrap)
            _context.Project.Logger?.Information(
                "Bootstrapped test infrastructure for project with no existing tests: {TestProjectPath}",
                plan.ProjectBootstrap?.TestProjectPath);
    }

    private async Task<string> ReadLatestErrorLogsAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_buildTestService.LatestLogPath) &&
            File.Exists(_buildTestService.LatestLogPath))
            return await File.ReadAllTextAsync(_buildTestService.LatestLogPath, cancellationToken);

        return "No error logs available";
    }

    private Task<string?> ReadLatestStructuredErrorsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_buildTestService.LatestStructuredErrors);
    }

    private static CandidateMethodInfo ToCandidateMethodInfo(CandidateMethodContext context)
    {
        return new CandidateMethodInfo(
            context.Method.MethodName,
            context.MethodSignature,
            context.Method.SourceCode,
            context.ContainingClass,
            context.TestClassName,
            context.TestFilePath,
            context.SourceProjectPath,
            context.TestProjectPath,
            context.TargetBuildFramework,
            context.SolutionFilePath,
            context.ExampleTest,
            context.ExampleTestMetadataSummary,
            context.ProjectTestMetadataSummary,
            context.TestClass,
            context.TestFileContents,
            context.TestSupportContext,
            context.TestFramework,
            context.TestDependencies,
            context.CoverageGapSummary,
            context.MutationSummary,
            context.Method.BaselineCoverage);
    }

    private static GenerationEvidencePackage CreateValidationEvidence(
        CandidateMethodContext context,
        Models.Configuration.Testing.Generation.GenerationConfig generationConfig)
    {
        return new GenerationEvidencePackage
        {
            Objective = generationConfig.Objective,
            Approach = generationConfig.Strategy,
            MetricsPath = generationConfig.Strategy == Models.Configuration.Testing.Generation.TestGenerationApproach.Naive
                ? null
                : generationConfig.MetricsPath,
            CandidateContext = context,
            StrategyInstruction = string.Empty
        };
    }

    private static TestGenerationRequest ApplyGenerationConfiguration(
        TestGenerationRequest request,
        Models.Configuration.Testing.Generation.GenerationConfig generationConfig)
    {
        return new TestGenerationRequest
        {
            Objective = generationConfig.Objective,
            Approach = generationConfig.Strategy,
            MetricsPath = generationConfig.Strategy == Models.Configuration.Testing.Generation.TestGenerationApproach.Naive
                ? null
                : generationConfig.MetricsPath,
            ContextMode = generationConfig.ContextMode,
            Steps = generationConfig.Steps,
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

    private static TestRepairRequest ApplyGenerationConfiguration(
        TestRepairRequest request,
        Models.Configuration.Testing.Generation.GenerationConfig generationConfig)
    {
        return new TestRepairRequest
        {
            Objective = generationConfig.Objective,
            Approach = generationConfig.Strategy,
            MetricsPath = generationConfig.Strategy == Models.Configuration.Testing.Generation.TestGenerationApproach.Naive
                ? null
                : generationConfig.MetricsPath,
            ContextMode = generationConfig.ContextMode,
            Steps = generationConfig.Steps,
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
}

/// <summary>
/// Information about a candidate method for test generation.
/// </summary>
internal record CandidateMethodInfo(
    string MethodName,
    string MethodSignature,
    string MethodBody,
    string ContainingClass,
    string TestClassName,
    string TestFilePath,
    string SourceProjectPath,
    string TestProjectPath,
    string TargetBuildFramework,
    string SolutionFilePath,
    string ExampleTest,
    string ExampleTestMetadataSummary,
    string ProjectTestMetadataSummary,
    string TestClass,
    string TestFileContents,
    string TestSupportContext,
    string TestFramework,
    string TestDependencies,
    string CoverageGapSummary,
    string MutationSummary,
    double BaselineCoverage
);
