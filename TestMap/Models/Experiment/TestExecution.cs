namespace TestMap.Models.Experiment;

/// <summary>
/// Represents the execution result of a generated test.
/// </summary>
public class TestExecution
{
    public int Id { get; set; }
    public int GenerationAttemptId { get; set; }
    public string? GeneratedTestCode { get; set; }
    public string? GeneratedTestMethodName { get; set; }
    public bool CompilationSuccess { get; set; }
    public bool TestsExecuted { get; set; }
    public bool TestPassed { get; set; }
    public double CoverageAfter { get; set; }
    public double CoverageImprovement { get; set; }
    public double? BaselineMutationScore { get; set; }
    public double? MutationScoreAfter { get; set; }
    public double? MutationScoreImprovement { get; set; }
    public TestClassification Classification { get; set; } = TestClassification.ValidationFailed;
    public string ValidationResultJson { get; set; } = string.Empty;
    public bool? Accepted { get; set; }
    public string? AcceptanceReason { get; set; }
    public string ValidationRuleDecisionJson { get; set; } = string.Empty;
    public string ClassificationRuleDecisionJson { get; set; } = string.Empty;
    public TestFailureKind FailureKind { get; set; } = TestFailureKind.None;
    public string? CompilationErrors { get; set; }
    public string? RuntimeErrors { get; set; }
    public string? AssertionErrors { get; set; }
    public string? FailureStage { get; set; }
    public string? FailureCategory { get; set; }
    public string? FailureSummary { get; set; }
    public string? StructuredErrors { get; set; }
    public string? ErrorLogs { get; set; }
    public bool RoslynValidationSucceeded { get; set; } = true;
    public bool RoslynValidationSkipped { get; set; }
    public int RoslynDiagnosticsBeforeCount { get; set; }
    public int RoslynDiagnosticsAfterCount { get; set; }
    public int NewRoslynDiagnosticsCount { get; set; }
    public string? NewRoslynDiagnostics { get; set; }
    public long? ExecutionTimeMs { get; set; }
    public DateTime ExecutedAt { get; set; }

    public virtual GenerationAttempt? GenerationAttempt { get; set; }
}
