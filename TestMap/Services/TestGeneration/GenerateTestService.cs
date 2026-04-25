using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;
using TestMap.Models.Results;
using TestMap.Services.Configuration;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration.Bootstrap;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.Strategies;
using TestMap.Services.TestGeneration.TargetSelection;
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

    private readonly
        IReadOnlyDictionary<Models.Configuration.Testing.Generation.TestActionExecutorMode, ITestActionExecutor>
        _actionExecutors;

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
        IEnumerable<ITestActionExecutor> actionExecutors,
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
        _actionExecutors = actionExecutors.ToDictionary(x => x.Mode);
        _workspace = workspace;
    }

    public async Task GenerateTestAsync()
    {
        _context.Project.Logger?.Information("Starting test generation using decomposed pipeline...");

        var generationConfig = _config.TestingConfig.GenerationConfig;
        var provider = generationConfig.Provider;
        var maxRetries = generationConfig.MaxRetries;
        var generationApproach = ResolveGenerationApproach(generationConfig.Strategy);
        var actionExecutor = ResolveActionExecutor(generationConfig.Executor);

        ValidateGenerationConfiguration(provider, maxRetries);
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
                maxRetries,
                generationApproach,
                actionExecutor);
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
        int maxRetries,
        ITestGenerationApproach generationApproach,
        ITestActionExecutor actionExecutor,
        CancellationToken cancellationToken = default)
    {
        string? candidateTest = null;
        string? resultHistory = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            _context.Project.Logger?.Information(
                "[Attempt {Attempt}/{MaxRetries}] Generating test for {MethodName}",
                attempt,
                maxRetries,
                method.MethodName);

            TestGenerationResult result;

            if (attempt == 1)
            {
                var request = generationApproach.CreateGenerationRequest(new TestGenerationApproachContext
                {
                    MethodContext = methodContext,
                    Provider = provider,
                    Temperature = 0.0,
                    EnableHistoryChaining = _config.TestingConfig.GenerationConfig.EnableHistoryChaining
                });

                result = await _pipelineService.GenerateTestAsync(request, cancellationToken);
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
                    Temperature = 0.0,
                    AttemptNumber = attempt,
                    EnableHistoryChaining = _config.TestingConfig.GenerationConfig.EnableHistoryChaining
                });

                result = await _pipelineService.RepairTestAsync(repairRequest, cancellationToken);
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

            var actionResult = await actionExecutor.ExecuteAsync(
                methodContext,
                candidateTest,
                testMethodName,
                cancellationToken);

            if (!actionResult.Success)
            {
                _context.Project.Logger?.Warning(
                    "{ErrorMessage}",
                    actionResult.ErrorMessage ?? $"Failed to apply generated test for {method.MethodName}.");
                await _workspace.RollbackChangesAsync(cancellationToken);
                continue;
            }

            if (!File.Exists(method.TestFilePath))
            {
                _context.Project.Logger?.Warning(
                    "Expected test file for {MethodName} was not found after applying changes: {TestFilePath}",
                    method.MethodName,
                    method.TestFilePath);
                await _workspace.RollbackChangesAsync(cancellationToken);
                continue;
            }

            var validationResult = await _buildTestService.ValidateBuildAsync(
                method.TestProjectPath,
                cancellationToken);

            if (!validationResult.IsSuccess)
            {
                _context.Project.Logger?.Warning(
                    "Docker compilation validation failed for {MethodName}: {Errors}",
                    method.MethodName,
                    validationResult.LogText);
                await _workspace.RollbackChangesAsync(cancellationToken);
                continue;
            }

            await RefreshProjectMetadataAsync(method.TestProjectPath);

            var buildResult = await _buildTestService.BuildTestAsync(
                BuildTestRunRequest.CreateIteration(
                    method.TestProjectPath,
                    method.TargetBuildFramework,
                    method.MethodName,
                    method.SourceProjectPath));

            var compilationSucceeded = DidCompilationSucceed(buildResult);
            var failedTests = buildResult.Results.Where(r => r.Outcome != "Passed").ToList();
            var newCoverage = buildResult is GeneratedTestRunModel generatedRun
                ? generatedRun.MethodCoverage
                : buildResult.Coverage / 100.0;
            var coverageImproved = newCoverage > method.BaselineCoverage;

            if (compilationSucceeded && buildResult.Results.Count > 0 && !failedTests.Any() && coverageImproved)
            {
                _context.Project.Logger?.Information(
                    "Test passed and improved coverage from {BaselineCoverage:P} to {NewCoverage:P}",
                    method.BaselineCoverage,
                    newCoverage);
                await _workspace.PersistAcceptedChangesAsync(
                    $"Add generated test for {method.MethodName}",
                    cancellationToken);
                return true;
            }

            _context.Project.Logger?.Warning(
                "Generated test for {MethodName} did not meet criteria. Compiled={Compiled}, Passed={Passed}, CoverageImproved={CoverageImproved}, Baseline={BaselineCoverage:P}, Current={NewCoverage:P}",
                method.MethodName,
                compilationSucceeded,
                compilationSucceeded && buildResult.Results.Count > 0 && !failedTests.Any(),
                coverageImproved,
                method.BaselineCoverage,
                newCoverage);

            await _workspace.RollbackChangesAsync(cancellationToken);
        }

        _context.Project.Logger?.Error(
            "Failed to generate a valid test for {MethodName} after {MaxRetries} attempts",
            method.MethodName,
            maxRetries);
        return false;
    }

    private ExperimentConfiguration CreateSelectionConfiguration()
    {
        var generationConfig = _config.TestingConfig.GenerationConfig;
        var experimentConfig = _config.ExperimentConfig;
        var candidateLimit = generationConfig.TargetSelection.CandidateLimit;
        var bootstrapLimit = generationConfig.Bootstrap.InitialCandidateLimit;

        if (_context.TestBootstrapState != null && bootstrapLimit > 0)
            candidateLimit = candidateLimit > 0
                ? Math.Min(candidateLimit, bootstrapLimit)
                : bootstrapLimit;

        return new ExperimentConfiguration
        {
            CandidateLimit = candidateLimit,
            MinCoverageThreshold = experimentConfig?.MinCoverageThreshold ?? 0.0,
            MaxCoverageThreshold = experimentConfig?.MaxCoverageThreshold ?? 0.99,
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

    private ITestActionExecutor ResolveActionExecutor(
        Models.Configuration.Testing.Generation.TestActionExecutorMode mode)
    {
        if (_actionExecutors.TryGetValue(mode, out var executor)) return executor;

        throw new InvalidOperationException($"No test action executor is registered for '{mode}'.");
    }

    private void ValidateGenerationConfiguration(AiProvider provider, int maxRetries)
    {
        if (maxRetries <= 0)
            throw new InvalidOperationException("TestingConfig.GenerationConfig.MaxRetries must be greater than 0.");

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
            context.Method.BaselineCoverage);
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
    double BaselineCoverage
);