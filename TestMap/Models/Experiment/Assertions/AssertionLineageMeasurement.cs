namespace TestMap.Models.Experiment.Assertions;

public sealed class AssertionLineageMeasurement
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int ExperimentRunId { get; set; }
    public int CandidateMethodId { get; set; }
    public int? GenerationAttemptId { get; set; }
    public int? ToolAttemptId { get; set; }
    public string ProducerLane { get; set; } = string.Empty;
    public AssertionMeasurementStatus Status { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public string AssertionCatalogVersion { get; set; } = string.Empty;
    public int MaxDepth { get; set; }
    public int? EligibleTestCount { get; set; }
    public int? AnalyzedTestCount { get; set; }
    public int? UnavailableTestCount { get; set; }
    public int? NoRecognizedAssertionTestCount { get; set; }
    public int? RecognizedAssertionCount { get; set; }
    public int? UnrecognizedAssertionCount { get; set; }
    public int? TracedAssertionCount { get; set; }
    public int? TrivialAssertionCount { get; set; }
    public int? UnresolvedAssertionCount { get; set; }
    public double? AnalysisDurationMs { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public IList<GeneratedTestAssertionSummary> TestSummaries { get; set; } =
        new List<GeneratedTestAssertionSummary>();
}
