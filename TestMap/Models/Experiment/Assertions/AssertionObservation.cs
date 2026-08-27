namespace TestMap.Models.Experiment.Assertions;

public sealed class AssertionObservation
{
    public int Id { get; set; }
    public int GeneratedTestAssertionSummaryId { get; set; }
    public int? InvocationId { get; set; }
    public int Ordinal { get; set; }
    public string Framework { get; set; } = string.Empty;
    public string AssertionMethod { get; set; } = string.Empty;
    public AssertionRecognitionKind RecognitionKind { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string ExpressionHash { get; set; } = string.Empty;
    public AssertionLineageCategory Category { get; set; }
    public string ResolutionCode { get; set; } = string.Empty;
    public int DepthReached { get; set; }
    public AssertionTargetRelation TargetRelation { get; set; }
    public string TraceSummary { get; set; } = string.Empty;
    public IList<AssertionLineageStep> Steps { get; set; } = new List<AssertionLineageStep>();
}
