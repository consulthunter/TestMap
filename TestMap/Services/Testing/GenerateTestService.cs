using LibGit2Sharp;
using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;
using TestMap.Models.Results;
using TestMap.Services.Configuration;
using TestMap.Services.Experiment;
using TestMap.Services.StaticAnalysis;

namespace TestMap.Services.Testing;

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
    private readonly IMethodSelectionService _methodSelectionService;
    private readonly IAnalyzeProjectService _analyzeProjectService;

    public GenerateTestService(
        ProjectContext context,
        TestMapConfig config,
        BuildTestService buildTestService,
        ITestGenerationPipelineService pipelineService,
        IMethodSelectionService methodSelectionService,
        IAnalyzeProjectService analyzeProjectService)
    {
        _context = context;
        _config = config;
        _buildTestService = buildTestService;
        _pipelineService = pipelineService;
        _methodSelectionService = methodSelectionService;
        _analyzeProjectService = analyzeProjectService;
    }

    public async Task GenerateTestAsync()
    {
        _context.Project.Logger?.Information("Starting test generation using decomposed pipeline...");

        var generationConfig = _config.TestingConfig.GenerationConfig;
        var provider = generationConfig.Provider;
        var maxRetries = generationConfig.MaxRetries;

        ValidateGenerationConfiguration(provider, maxRetries);

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

            var candidateInfo = ToCandidateMethodInfo(methodContext);

            if (!CanInsertIntoTestTarget(candidateInfo))
            {
                continue;
            }

            var succeeded = await GenerateTestForMethodAsync(candidateInfo, provider, maxRetries);
            if (succeeded)
            {
                successCount++;
            }
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
        CandidateMethodInfo method,
        AiProvider provider,
        int maxRetries,
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
                var request = new TestGenerationRequest
                {
                    MethodBody = method.MethodBody,
                    MethodName = method.MethodName,
                    MethodSignature = method.MethodSignature,
                    ContainingClass = method.ContainingClass,
                    ExampleTest = method.ExampleTest,
                    ExampleTestMetadataSummary = method.ExampleTestMetadataSummary,
                    ProjectTestMetadataSummary = method.ProjectTestMetadataSummary,
                    TestClass = method.TestClass,
                    TestFileContents = method.TestFileContents,
                    TestSupportContext = method.TestSupportContext,
                    TestFramework = method.TestFramework,
                    TestDependencies = method.TestDependencies,
                    CoverageGapSummary = method.CoverageGapSummary,
                    Provider = provider,
                    Temperature = 0.0,
                    EnableHistoryChaining = _config.TestingConfig.GenerationConfig.EnableHistoryChaining
                };

                result = await _pipelineService.GenerateTestAsync(request, cancellationToken);
            }
            else
            {
                var repairRequest = new TestRepairRequest
                {
                    MethodBody = method.MethodBody,
                    MethodName = method.MethodName,
                    GeneratedTest = candidateTest ?? string.Empty,
                    TestClass = method.TestClass,
                    TestFramework = method.TestFramework,
                    TestDependencies = method.TestDependencies,
                    TestFileContents = method.TestFileContents,
                    TestSupportContext = method.TestSupportContext,
                    ExampleTestMetadataSummary = method.ExampleTestMetadataSummary,
                    ProjectTestMetadataSummary = method.ProjectTestMetadataSummary,
                    CoverageGapSummary = method.CoverageGapSummary,
                    ErrorLogs = await ReadLatestErrorLogsAsync(cancellationToken),
                    StructuredErrors = await ReadLatestStructuredErrorsAsync(cancellationToken),
                    PriorConversationTranscript = resultHistory,
                    Provider = provider,
                    Temperature = 0.0,
                    AttemptNumber = attempt,
                    EnableHistoryChaining = _config.TestingConfig.GenerationConfig.EnableHistoryChaining
                };

                result = await _pipelineService.RepairTestAsync(repairRequest, cancellationToken);
            }

            if (!result.Success || string.IsNullOrWhiteSpace(result.GeneratedTest))
            {
                _context.Project.Logger?.Warning("Generation failed: {ErrorMessage}", result.ErrorMessage);
                RollbackRepo();
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
                RollbackRepo();
                continue;
            }

            _context.Project.Logger?.Information(
                "Generated test in {DurationSeconds:F2}s using {TotalTokens} tokens",
                result.TotalDurationSeconds,
                result.TotalTokens);

            if (!Utilities.Utilities.InsertTestIntoFile(method.TestClassName, method.TestFilePath, candidateTest))
            {
                _context.Project.Logger?.Warning(
                    "Failed to insert generated test for {MethodName} into {TestFilePath}",
                    method.MethodName,
                    method.TestFilePath);
                RollbackRepo();
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
                RollbackRepo();
                continue;
            }

            await RefreshProjectMetadataAsync(method.TestProjectPath);

            var buildResult = await _buildTestService.BuildTestAsync(
                BuildTestRunRequest.CreateIteration(
                    method.TestProjectPath,
                    method.TargetBuildFramework,
                    method.MethodName));

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

            RollbackRepo();
        }

        _context.Project.Logger?.Error(
            "Failed to generate a valid test for {MethodName} after {MaxRetries} attempts",
            method.MethodName,
            maxRetries);
        return false;
    }

    private ExperimentConfiguration CreateSelectionConfiguration()
    {
        var experimentConfig = _config.ExperimentConfig;
        if (experimentConfig != null)
        {
            return new ExperimentConfiguration
            {
                CandidateLimit = experimentConfig.CandidateLimit,
                MinCoverageThreshold = experimentConfig.MinCoverageThreshold,
                MaxCoverageThreshold = experimentConfig.MaxCoverageThreshold
            };
        }

        return new ExperimentConfiguration
        {
            CandidateLimit = 3,
            MinCoverageThreshold = 0.0,
            MaxCoverageThreshold = 0.99
        };
    }

    private void ValidateGenerationConfiguration(AiProvider provider, int maxRetries)
    {
        if (maxRetries <= 0)
        {
            throw new InvalidOperationException("TestingConfig.GenerationConfig.MaxRetries must be greater than 0.");
        }

        var providerConfig = _config.AiProviderConfig.GetProviderConfig(provider);
        if (providerConfig == null)
        {
            throw new InvalidOperationException($"Provider '{provider}' is not configured.");
        }

        var validationError = AiProviderConfigurationRules.GetValidationError(providerConfig);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            throw new InvalidOperationException(validationError);
        }
    }

    private bool CanInsertIntoTestTarget(CandidateMethodInfo method)
    {
        if (!File.Exists(method.TestFilePath))
        {
            _context.Project.Logger?.Warning(
                "Skipping method {MethodName} because test file was not found: {TestFilePath}",
                method.MethodName,
                method.TestFilePath);
            return false;
        }

        var fileContents = File.ReadAllText(method.TestFilePath);
        if (!fileContents.Contains($"class {method.TestClassName}", StringComparison.Ordinal))
        {
            _context.Project.Logger?.Warning(
                "Skipping method {MethodName} because test class {TestClassName} was not found in {TestFilePath}",
                method.MethodName,
                method.TestClassName,
                method.TestFilePath);
            return false;
        }

        return true;
    }

    private async Task<string> ReadLatestErrorLogsAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_buildTestService.LatestLogPath) &&
            File.Exists(_buildTestService.LatestLogPath))
        {
            return await File.ReadAllTextAsync(_buildTestService.LatestLogPath, cancellationToken);
        }

        return "No error logs available";
    }

    private Task<string?> ReadLatestStructuredErrorsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_buildTestService.LatestStructuredErrors);
    }

    private async Task RefreshProjectMetadataAsync(string testProjectPath)
    {
        var analysisProject = _context.Project.Projects.FirstOrDefault(x =>
            string.Equals(Path.GetFullPath(x.FilePath), Path.GetFullPath(testProjectPath), StringComparison.OrdinalIgnoreCase));

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

    private void RollbackRepo()
    {
        using var repo = new Repository(_context.Project.DirectoryPath);
        var headCommit = repo.Head.Tip;
        repo.Reset(ResetMode.Hard, headCommit);

        var status = repo.RetrieveStatus(new StatusOptions());
        foreach (var untracked in status.Untracked)
        {
            var path = Path.Combine(_context.Project.DirectoryPath, untracked.FilePath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        _context.Project.Logger?.Information("Repository rolled back to last commit.");
    }

    private static bool DidCompilationSucceed(TestRunModel buildResult)
    {
        if (buildResult.Results.Count == 0 && buildResult.Coverage == 0)
        {
            return false;
        }

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
