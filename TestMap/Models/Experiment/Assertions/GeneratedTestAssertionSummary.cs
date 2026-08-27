namespace TestMap.Models.Experiment.Assertions;

public sealed class GeneratedTestAssertionSummary
{
    public int Id { get; set; }
    public int AssertionLineageMeasurementId { get; set; }
    public int? TestMemberId { get; set; }
    public int? GeneratedTestExecutionId { get; set; }
    public int? ToolAttemptGeneratedTestId { get; set; }
    public string TestMethodName { get; set; } = string.Empty;
    public string TestFilePath { get; set; } = string.Empty;
    public string TestMemberContentHash { get; set; } = string.Empty;
    public string FallbackIdentityHash { get; set; } = string.Empty;
    public GeneratedTestAssertionStatus Status { get; set; }
    public string? StatusReason { get; set; }
    public int? RecognizedAssertionCount { get; set; }
    public int? UnrecognizedAssertionCount { get; set; }
    public int? TracedAssertionCount { get; set; }
    public int? TrivialAssertionCount { get; set; }
    public int? UnresolvedAssertionCount { get; set; }
    public IList<AssertionObservation> Observations { get; set; } = new List<AssertionObservation>();
}
