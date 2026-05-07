using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Models.Testing;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.Services.TestGeneration.Execution;

public interface IGeneratedTestExecutionService
{
    Task<GeneratedTestExecutionResult> ExecuteAsync(
        CandidateMethodContext context,
        string generatedTest,
        string testMethodName,
        TestActionExecutorMode? mode = null,
        CancellationToken cancellationToken = default);
}

public sealed class GeneratedTestExecutionResult
{
    public string GeneratedTestCode { get; init; } = string.Empty;
    public string GeneratedTestMethodName { get; init; } = string.Empty;
    public bool CodeExtracted { get; init; }
    public bool MethodNameExtracted { get; init; }
    public bool SyntaxValid { get; init; } = true;
    public bool ApplicationSucceeded { get; init; }
    public string? AppliedFilePath { get; init; }
    public CandidateActionKind ActionKind { get; init; }
    public bool CompilationSucceeded { get; init; }
    public bool TestsExecuted { get; init; }
    public bool AllTestsPassed { get; init; }
    public int FailedTestCount { get; init; }
    public double BaselineCoverage { get; init; }
    public double CoverageAfter { get; init; }
    public double CoverageImprovement { get; init; }
    public double? BaselineMutationScore { get; init; }
    public double? MutationScoreAfter { get; init; }
    public double? MutationScoreImprovement { get; init; }
    public TestFailureKind FailureKind { get; init; } = TestFailureKind.None;
    public string? CompilationErrors { get; init; }
    public string? RuntimeErrors { get; init; }
    public string? AssertionErrors { get; init; }
    public string? StructuredErrors { get; init; }
    public string? ErrorLogs { get; init; }
    public string? FailureStage { get; init; }
    public string? FailureCategory { get; init; }
    public string? FailureSummary { get; init; }
    public bool RoslynValidationSucceeded { get; init; } = true;
    public bool RoslynValidationSkipped { get; init; }
    public IReadOnlyList<RoslynDiagnosticSnapshot> RoslynDiagnosticsBefore { get; init; } = [];
    public IReadOnlyList<RoslynDiagnosticSnapshot> RoslynDiagnosticsAfter { get; init; } = [];
    public IReadOnlyList<RoslynDiagnosticSnapshot> NewRoslynDiagnostics { get; init; } = [];
    public IReadOnlyList<RuleDecisionRecord> ApplicationRuleDecisions { get; init; } = [];
    public IReadOnlyList<RuleDecisionRecord> RoslynPreBuildRuleDecisions { get; init; } = [];
    public TestRunModel? TestRun { get; init; }
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
}
