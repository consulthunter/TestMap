namespace TestMap.Models.Experiment.Assertions;

public sealed class AssertionLineageStep
{
    public int Id { get; set; }
    public int AssertionObservationId { get; set; }
    public int InputIndex { get; set; }
    public int PathIndex { get; set; }
    public int StepIndex { get; set; }
    public AssertionLineageStepKind StepKind { get; set; }
    public int Depth { get; set; }
    public string SymbolDisplay { get; set; } = string.Empty;
    public int? MemberId { get; set; }
    public string? FilePath { get; set; }
    public int? StartLine { get; set; }
    public int? StartColumn { get; set; }
    public int? EndLine { get; set; }
    public int? EndColumn { get; set; }
    public AssertionLineageStepOutcome Outcome { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
