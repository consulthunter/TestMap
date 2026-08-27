using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment.Assertions;

public sealed class AssertionLineageStepEntity
{
    public int Id { get; set; }
    public int AssertionObservationId { get; set; }
    public int InputIndex { get; set; }
    public int PathIndex { get; set; }
    public int StepIndex { get; set; }
    [MaxLength(100)] public string StepKind { get; set; } = string.Empty;
    public int Depth { get; set; }
    public string SymbolDisplay { get; set; } = string.Empty;
    public int? MemberId { get; set; }
    public string? FilePath { get; set; }
    public int? StartLine { get; set; }
    public int? StartColumn { get; set; }
    public int? EndLine { get; set; }
    public int? EndColumn { get; set; }
    [MaxLength(50)] public string Outcome { get; set; } = string.Empty;
    [MaxLength(100)] public string ReasonCode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    public AssertionObservationEntity? AssertionObservation { get; set; }
}
