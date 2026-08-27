using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment.Assertions;

public sealed class AssertionObservationEntity
{
    public int Id { get; set; }
    public int GeneratedTestAssertionSummaryId { get; set; }
    public int? InvocationId { get; set; }
    public int Ordinal { get; set; }
    [MaxLength(100)] public string Framework { get; set; } = string.Empty;
    public string AssertionMethod { get; set; } = string.Empty;
    [MaxLength(50)] public string RecognitionKind { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    [MaxLength(64)] public string ExpressionHash { get; set; } = string.Empty;
    [MaxLength(50)] public string Category { get; set; } = string.Empty;
    [MaxLength(100)] public string ResolutionCode { get; set; } = string.Empty;
    public int DepthReached { get; set; }
    [MaxLength(50)] public string TargetRelation { get; set; } = string.Empty;
    public string TraceSummary { get; set; } = string.Empty;

    public GeneratedTestAssertionSummaryEntity? GeneratedTestAssertionSummary { get; set; }
    public ICollection<AssertionLineageStepEntity> LineageSteps { get; set; } =
        new List<AssertionLineageStepEntity>();
}
