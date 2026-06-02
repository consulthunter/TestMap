using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Results;
using TestMap.Models.Testing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models.Code;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.StaticAnalysis.Enrichment;
using TestMap.Services.TestExecution;
using TestMap.Services.TestGeneration;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.Validation;
using TestMap.Persistence.Ef.Repositories.Testing;
using TestMap.Persistence.Ef.Repositories.MutationTesting;

namespace TestMap.Services.TestGeneration.Execution;

public sealed class GeneratedTestExecutionService : IGeneratedTestExecutionService
{
    private readonly ProjectContext _context;
    private readonly TestMapConfig _config;
    private readonly BuildTestService _buildTestService;
    private readonly IGeneratedTestApplicationService _applicationService;
    private readonly IAnalyzeProjectService _analyzeProjectService;
    private readonly ICodeMetricsService _codeMetricsService;
    private readonly IRoslynGeneratedTestValidationService _roslynValidationService;
    private readonly ITestSmellService _testSmellService;
    private readonly TestRunRepository _testRunRepository;
    private readonly MutationTestingReportRepository _mutationTestingReportRepository;

    public GeneratedTestExecutionService(
        ProjectContext context,
        TestMapConfig config,
        BuildTestService buildTestService,
        IGeneratedTestApplicationService applicationService,
        IAnalyzeProjectService analyzeProjectService,
        ICodeMetricsService codeMetricsService,
        IRoslynGeneratedTestValidationService roslynValidationService,
        ITestSmellService testSmellService,
        TestRunRepository testRunRepository,
        MutationTestingReportRepository mutationTestingReportRepository)
    {
        _context = context;
        _config = config;
        _buildTestService = buildTestService;
        _applicationService = applicationService;
        _analyzeProjectService = analyzeProjectService;
        _codeMetricsService = codeMetricsService;
        _roslynValidationService = roslynValidationService;
        _testSmellService = testSmellService;
        _testRunRepository = testRunRepository;
        _mutationTestingReportRepository = mutationTestingReportRepository;
    }

    public async Task<GeneratedTestExecutionResult> ExecuteAsync(
        CandidateMethodContext context,
        string generatedTest,
        string testMethodName,
        TestActionExecutorMode? mode = null,
        int? experimentRunId = null,
        CancellationToken cancellationToken = default)
    {
        var executionMode = mode ??
                            GenerationObjectivePolicy.ResolveExecutor(_config.TestingConfig.GenerationConfig.Objective);

        if (string.IsNullOrWhiteSpace(generatedTest) || string.IsNullOrWhiteSpace(testMethodName))
        {
            return new GeneratedTestExecutionResult
            {
                GeneratedTestCode = generatedTest,
                GeneratedTestMethodName = testMethodName,
                CodeExtracted = !string.IsNullOrWhiteSpace(generatedTest),
                MethodNameExtracted = !string.IsNullOrWhiteSpace(testMethodName),
                BaselineCoverage = context.Method.BaselineCoverage,
                FailureKind = TestFailureKind.Generation,
                FailureStage = "generation",
                FailureCategory = "generated_test_missing",
                FailureSummary = "Generated test code or method name was missing."
            };
        }

        try
        {
            var roslynBefore = _config.TestingConfig.GenerationConfig.Steps.EnableRoslynValidation
                ? await _roslynValidationService.CaptureBeforeAsync(context, cancellationToken)
                : RoslynGeneratedTestDiagnosticSnapshot.Skip("Roslyn validation is disabled by generation step configuration.");

            var baselineRun = await RunScopedValidationMetricsAsync(
                context,
                experimentRunId,
                isMutationBaseline: true);
            if (!IsUsableScopedMetricRun(baselineRun))
            {
                return new GeneratedTestExecutionResult
                {
                    GeneratedTestCode = generatedTest,
                    GeneratedTestMethodName = testMethodName,
                    CodeExtracted = true,
                    MethodNameExtracted = true,
                    BaselineCoverage = ResolveScopedMethodCoverage(baselineRun),
                    BaselineMutationScore = baselineRun.MutationScore,
                    TestRun = baselineRun,
                    FailureKind = TestFailureKind.Infrastructure,
                    RuntimeErrors = baselineRun.FailureAnalysis?.Evidence,
                    ErrorLogs = baselineRun.FailureAnalysis?.Evidence,
                    FailureStage = baselineRun.FailureAnalysis?.Stage ?? "pre-integration-validation",
                    FailureCategory = baselineRun.FailureAnalysis?.Category ?? "pre_integration_scoped_metrics_failed",
                    FailureSummary = baselineRun.FailureAnalysis?.Summary ??
                                     "Targeted pre-integration coverage/mutation validation failed before applying the generated test."
                };
            }

            var actionResult = await _applicationService.ApplyAsync(
                context,
                generatedTest,
                testMethodName,
                executionMode,
                cancellationToken);

            if (!actionResult.Success)
            {
                return new GeneratedTestExecutionResult
                {
                    GeneratedTestCode = generatedTest,
                    GeneratedTestMethodName = testMethodName,
                    CodeExtracted = true,
                    MethodNameExtracted = true,
                    ApplicationSucceeded = false,
                    BaselineCoverage = ResolveScopedMethodCoverage(baselineRun),
                    BaselineMutationScore = baselineRun.MutationScore,
                    TestRun = baselineRun,
                    ActionKind = actionResult.ActionKind,
                    ApplicationRuleDecisions = actionResult.RuleDecisions,
                    FailureKind = TestFailureKind.Generation,
                    FailureStage = "application",
                    FailureCategory = "generated_test_application_failed",
                    FailureSummary = actionResult.ErrorMessage,
                    ErrorLogs = actionResult.ErrorMessage
                };
            }

            if (!AppliedTestMethodExists(actionResult.AppliedFilePath, testMethodName))
            {
                var errorMessage =
                    $"Generated test application reported success, but method '{testMethodName}' was not found in '{actionResult.AppliedFilePath}'.";
                _context.Project.Logger?.Warning(errorMessage);

                return new GeneratedTestExecutionResult
                {
                    GeneratedTestCode = generatedTest,
                    GeneratedTestMethodName = testMethodName,
                    CodeExtracted = true,
                    MethodNameExtracted = true,
                    ApplicationSucceeded = false,
                    AppliedFilePath = actionResult.AppliedFilePath,
                    ActionKind = actionResult.ActionKind,
                    ApplicationRuleDecisions = actionResult.RuleDecisions,
                    BaselineCoverage = ResolveScopedMethodCoverage(baselineRun),
                    BaselineMutationScore = baselineRun.MutationScore,
                    TestRun = baselineRun,
                    FailureKind = TestFailureKind.Generation,
                    FailureStage = "application",
                    FailureCategory = "generated_test_method_not_applied",
                    FailureSummary = errorMessage,
                    ErrorLogs = errorMessage
                };
            }

            _context.Project.Logger?.Information(
                "Generated test method '{GeneratedTestMethodName}' applied to {AppliedFilePath}.",
                testMethodName,
                actionResult.AppliedFilePath);

            var roslynValidation = _config.TestingConfig.GenerationConfig.Steps.EnableRoslynValidation
                ? await _roslynValidationService.ValidateAfterApplicationAsync(
                    context,
                    roslynBefore,
                    cancellationToken)
                : RoslynGeneratedTestValidationResult.Skip("Roslyn validation is disabled by generation step configuration.");

            if (!roslynValidation.Succeeded)
                _context.Project.Logger?.Warning(
                    "Roslyn validation reported diagnostics after generated test application. Summary={FailureSummary}",
                    roslynValidation.FailureSummary);

            var preBuildDecision = RoslynPreBuildDecisionClassifier.Classify(roslynValidation);
            if (!preBuildDecision.ShouldBuild)
            {
                _context.Project.Logger?.Warning(
                    "Skipping real build validation because Roslyn pre-build gate found a high-confidence local generated-test defect. Reason={Reason}",
                    preBuildDecision.Reason);

                return new GeneratedTestExecutionResult
                {
                    GeneratedTestCode = generatedTest,
                    GeneratedTestMethodName = testMethodName,
                    CodeExtracted = true,
                    MethodNameExtracted = true,
                    ApplicationSucceeded = true,
                    AppliedFilePath = actionResult.AppliedFilePath,
                    ActionKind = actionResult.ActionKind,
                    ApplicationRuleDecisions = actionResult.RuleDecisions,
                    RoslynPreBuildRuleDecisions = preBuildDecision.RuleDecisions,
                    BaselineCoverage = ResolveScopedMethodCoverage(baselineRun),
                    BaselineMutationScore = baselineRun.MutationScore,
                    TestRun = baselineRun,
                    CompilationSucceeded = false,
                    RoslynValidationSucceeded = roslynValidation.Succeeded,
                    RoslynValidationSkipped = roslynValidation.Skipped,
                    RoslynDiagnosticsBefore = roslynValidation.Before.Diagnostics,
                    RoslynDiagnosticsAfter = roslynValidation.After.Diagnostics,
                    NewRoslynDiagnostics = roslynValidation.NewDiagnostics,
                    FailureKind = TestFailureKind.Compilation,
                    CompilationErrors = FormatDiagnostics(roslynValidation.NewDiagnostics),
                    ErrorLogs = FormatDiagnostics(roslynValidation.NewDiagnostics),
                    FailureStage = "roslyn-pre-build",
                    FailureCategory = preBuildDecision.FailureClass?.ToString() ?? "roslyn_pre_build_rejected",
                    FailureSummary = preBuildDecision.Reason
                };
            }

            var validationResult = await _buildTestService.ValidateBuildAsync(
                context.TestProjectPath,
                cancellationToken);

            if (!validationResult.IsSuccess)
            {
                return new GeneratedTestExecutionResult
                {
                    GeneratedTestCode = generatedTest,
                    GeneratedTestMethodName = testMethodName,
                    CodeExtracted = true,
                    MethodNameExtracted = true,
                    ApplicationSucceeded = true,
                    AppliedFilePath = actionResult.AppliedFilePath,
                    ActionKind = actionResult.ActionKind,
                    ApplicationRuleDecisions = actionResult.RuleDecisions,
                    RoslynPreBuildRuleDecisions = preBuildDecision.RuleDecisions,
                    BaselineCoverage = ResolveScopedMethodCoverage(baselineRun),
                    BaselineMutationScore = baselineRun.MutationScore,
                    TestRun = baselineRun,
                    CompilationSucceeded = false,
                    RoslynValidationSucceeded = roslynValidation.Succeeded,
                    RoslynValidationSkipped = roslynValidation.Skipped,
                    RoslynDiagnosticsBefore = roslynValidation.Before.Diagnostics,
                    RoslynDiagnosticsAfter = roslynValidation.After.Diagnostics,
                    NewRoslynDiagnostics = roslynValidation.NewDiagnostics,
                    FailureKind = TestFailureKind.Compilation,
                    CompilationErrors = validationResult.LogText,
                    StructuredErrors = validationResult.StructuredErrors,
                    ErrorLogs = validationResult.LogText,
                    FailureStage = "build",
                    FailureCategory = "docker_compilation_validation_failed",
                    FailureSummary = "Docker build validation failed before test execution."
                };
            }

            await RefreshProjectMetadataAsync(context.TestProjectPath, cancellationToken);

            var buildResult = await RunScopedValidationMetricsAsync(
                context,
                experimentRunId,
                isMutationBaseline: false);

            return CreateExecutionResult(
                context,
                generatedTest,
                testMethodName,
                actionResult,
                buildResult,
                roslynValidation,
                preBuildDecision,
                ResolveScopedMethodCoverage(baselineRun),
                baselineRun.MutationScore);
        }
        catch (Exception ex)
        {
            return new GeneratedTestExecutionResult
            {
                GeneratedTestCode = generatedTest,
                GeneratedTestMethodName = testMethodName,
                CodeExtracted = true,
                MethodNameExtracted = true,
                BaselineCoverage = context.Method.BaselineCoverage,
                FailureKind = TestFailureKind.Infrastructure,
                RuntimeErrors = ex.Message,
                ErrorLogs = ex.ToString(),
                FailureStage = "execution",
                FailureCategory = "unexpected_execution_exception",
                FailureSummary = "An unexpected exception occurred while executing the generated test."
            };
        }
    }

    private GeneratedTestExecutionResult CreateExecutionResult(
        CandidateMethodContext context,
        string generatedTest,
        string testMethodName,
        GeneratedTestApplicationResult actionResult,
        TestRunModel buildResult,
        RoslynGeneratedTestValidationResult roslynValidation,
        RoslynPreBuildDecision preBuildDecision,
        double baselineCoverage,
        double? baselineMutationScore)
    {
        var compilationSucceeded = DidCompilationSucceed(buildResult);
        var failedTests = buildResult.Results.Where(x => x.Outcome != "Passed").ToList();
        var testsExecuted = buildResult.Results.Count > 0;
        var allTestsPassed = compilationSucceeded && testsExecuted && failedTests.Count == 0;
        var coverageAfter = buildResult is GeneratedTestRunModel generatedRun
            ? generatedRun.MethodCoverage
            : buildResult.Coverage / 100.0;
        var coverageImprovement = coverageAfter - baselineCoverage;
        var mutationScoreImprovement = CalculateMutationScoreImprovement(
            baselineMutationScore,
            buildResult.MutationScore);

        var result = new GeneratedTestExecutionResult
        {
            GeneratedTestCode = generatedTest,
            GeneratedTestMethodName = testMethodName,
            CodeExtracted = true,
            MethodNameExtracted = true,
            ApplicationSucceeded = true,
            AppliedFilePath = actionResult.AppliedFilePath,
            ActionKind = actionResult.ActionKind,
            ApplicationRuleDecisions = actionResult.RuleDecisions,
            RoslynPreBuildRuleDecisions = preBuildDecision.RuleDecisions,
            CompilationSucceeded = compilationSucceeded,
            TestsExecuted = testsExecuted,
            AllTestsPassed = allTestsPassed,
            FailedTestCount = failedTests.Count,
            BaselineCoverage = baselineCoverage,
            CoverageAfter = coverageAfter,
            CoverageImprovement = coverageImprovement,
            BaselineMutationScore = baselineMutationScore,
            MutationScoreAfter = buildResult.MutationScore,
            MutationScoreImprovement = mutationScoreImprovement,
            RoslynValidationSucceeded = roslynValidation.Succeeded,
            RoslynValidationSkipped = roslynValidation.Skipped,
            RoslynDiagnosticsBefore = roslynValidation.Before.Diagnostics,
            RoslynDiagnosticsAfter = roslynValidation.After.Diagnostics,
            NewRoslynDiagnostics = roslynValidation.NewDiagnostics,
            TestRun = buildResult,
            FailureKind = TestFailureKind.None
        };

        if (!compilationSucceeded)
            return result.WithFailure(
                buildResult.FailureAnalysis != null ? MapFailureKind(buildResult.FailureAnalysis) : TestFailureKind.Compilation,
                buildResult.FailureAnalysis?.Evidence,
                null,
                null,
                buildResult.FailureAnalysis?.Stage ?? "build",
                buildResult.FailureAnalysis?.Category ?? "build_failed",
                buildResult.FailureAnalysis?.Summary ?? "Build failed before test execution.");

        if (buildResult.FailureAnalysis != null && !testsExecuted)
            return result.WithFailure(
                MapFailureKind(buildResult.FailureAnalysis),
                null,
                buildResult.FailureAnalysis.Evidence,
                null,
                buildResult.FailureAnalysis.Stage,
                buildResult.FailureAnalysis.Category,
                buildResult.FailureAnalysis.Summary);

        if (!allTestsPassed)
        {
            var errors = string.Join(
                Environment.NewLine,
                failedTests.Select(x => x.ErrorMessage).Where(x => !string.IsNullOrWhiteSpace(x)));

            return result.WithFailure(
                TestFailureKind.Runtime,
                null,
                errors,
                errors,
                "test",
                "test_failed",
                $"{failedTests.Count} generated test assertion or execution failure(s).");
        }

        return result;
    }

    private static double? CalculateMutationScoreImprovement(double? baselineMutationScore, double? mutationScoreAfter)
    {
        return baselineMutationScore.HasValue && mutationScoreAfter.HasValue
            ? mutationScoreAfter.Value - baselineMutationScore.Value
            : null;
    }

    private Task<TestRunModel> RunScopedValidationMetricsAsync(
        CandidateMethodContext context,
        int? experimentRunId,
        bool isMutationBaseline)
    {
        return _buildTestService.BuildTestAsync(
            BuildTestRunRequest.CreateIteration(
                context.TestProjectPath,
                context.TargetBuildFramework,
                context.Method.MethodName,
                string.IsNullOrWhiteSpace(context.SourceProjectPath) ? null : context.SourceProjectPath,
                experimentRunId,
                isMutationBaseline));
    }

    private static double ResolveScopedMethodCoverage(TestRunModel run)
    {
        return run is GeneratedTestRunModel generatedRun
            ? generatedRun.MethodCoverage
            : run.Coverage / 100.0;
    }

    private static bool IsUsableScopedMetricRun(TestRunModel run)
    {
        return DidCompilationSucceed(run) &&
               run.Results.Count > 0 &&
               run.FailureAnalysis == null;
    }

    private async Task RefreshProjectMetadataAsync(
        string testProjectPath,
        CancellationToken cancellationToken)
    {
        var analysisProject = _context.Project.Projects.FirstOrDefault(x =>
            string.Equals(Path.GetFullPath(x.FilePath), Path.GetFullPath(testProjectPath),
                StringComparison.OrdinalIgnoreCase));

        if (analysisProject == null) return;

        // Analyze all projects in topological order with a shared member ID dictionary so
        // that cross-project invocations from the re-analyzed test project can resolve
        // production member IDs. Production projects are upserted idempotently (unchanged
        // content → same DB ID returned) and then the test project is analyzed with its
        // newly-applied generated test, producing correct cross-project Invocation edges.
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in TopologicallySorted(_context.Project.Projects))
            await _analyzeProjectService.AnalyzeProjectAsync(project, sharedMemberIds);

        await _codeMetricsService.CollectCodeMetricsAsync(analysisProject, cancellationToken);

        if (_context.Project.DbId != 0)
            await _testSmellService.CollectAsync(testProjectPath, _context.Project.DbId, cancellationToken);
    }

    /// <summary>
    /// Returns projects sorted so every dependency comes before the projects that reference
    /// it (dependencies-first topological order). Mirrors <c>AnalyzeProjectStep.TopologicallySorted</c>.
    /// </summary>
    private static IEnumerable<CSharpProjectModel> TopologicallySorted(
        IReadOnlyList<CSharpProjectModel> projects)
    {
        var byPath = projects.ToDictionary(
            p => p.FilePath,
            p => p,
            StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CSharpProjectModel>(projects.Count);

        void Visit(CSharpProjectModel project)
        {
            if (!visited.Add(project.FilePath)) return;
            foreach (var refPath in project.ProjectReferences)
                if (byPath.TryGetValue(refPath, out var dependency))
                    Visit(dependency);
            result.Add(project);
        }

        foreach (var project in projects)
            Visit(project);

        return result;
    }

    private static bool AppliedTestMethodExists(string? appliedFilePath, string testMethodName)
    {
        if (string.IsNullOrWhiteSpace(appliedFilePath) ||
            string.IsNullOrWhiteSpace(testMethodName) ||
            !File.Exists(appliedFilePath))
            return false;

        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(appliedFilePath)).GetCompilationUnitRoot();
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Any(x => string.Equals(x.Identifier.Text, testMethodName, StringComparison.Ordinal));
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

    private static string FormatDiagnostics(IReadOnlyList<RoslynDiagnosticSnapshot> diagnostics)
    {
        if (diagnostics.Count == 0) return string.Empty;

        return string.Join(
            Environment.NewLine,
            diagnostics.Select(x =>
                $"{x.Id} {x.Severity} {x.FilePath ?? string.Empty}({x.StartLine + 1},{x.StartColumn + 1}): {x.Message}"));
    }
}

internal static class GeneratedTestExecutionResultExtensions
{
    public static GeneratedTestExecutionResult WithFailure(
        this GeneratedTestExecutionResult result,
        TestFailureKind failureKind,
        string? compilationErrors,
        string? runtimeErrors,
        string? assertionErrors,
        string failureStage,
        string failureCategory,
        string failureSummary)
    {
        return new GeneratedTestExecutionResult
        {
            GeneratedTestCode = result.GeneratedTestCode,
            GeneratedTestMethodName = result.GeneratedTestMethodName,
            CodeExtracted = result.CodeExtracted,
            MethodNameExtracted = result.MethodNameExtracted,
            SyntaxValid = result.SyntaxValid,
            ApplicationSucceeded = result.ApplicationSucceeded,
            AppliedFilePath = result.AppliedFilePath,
            ActionKind = result.ActionKind,
            CompilationSucceeded = result.CompilationSucceeded,
            TestsExecuted = result.TestsExecuted,
            AllTestsPassed = false,
            FailedTestCount = result.FailedTestCount,
            BaselineCoverage = result.BaselineCoverage,
            CoverageAfter = result.CoverageAfter,
            CoverageImprovement = result.CoverageImprovement,
            BaselineMutationScore = result.BaselineMutationScore,
            MutationScoreAfter = result.MutationScoreAfter,
            MutationScoreImprovement = result.MutationScoreImprovement,
            FailureKind = failureKind,
            CompilationErrors = compilationErrors,
            RuntimeErrors = runtimeErrors,
            AssertionErrors = assertionErrors,
            ErrorLogs = runtimeErrors ?? compilationErrors,
            FailureStage = failureStage,
            FailureCategory = failureCategory,
            FailureSummary = failureSummary,
            RoslynValidationSucceeded = result.RoslynValidationSucceeded,
            RoslynValidationSkipped = result.RoslynValidationSkipped,
            RoslynDiagnosticsBefore = result.RoslynDiagnosticsBefore,
            RoslynDiagnosticsAfter = result.RoslynDiagnosticsAfter,
            NewRoslynDiagnostics = result.NewRoslynDiagnostics,
            ApplicationRuleDecisions = result.ApplicationRuleDecisions,
            RoslynPreBuildRuleDecisions = result.RoslynPreBuildRuleDecisions,
            TestRun = result.TestRun,
            ExecutedAt = result.ExecutedAt
        };
    }
}
