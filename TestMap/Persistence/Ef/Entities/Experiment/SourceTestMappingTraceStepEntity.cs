using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class SourceTestMappingTraceStepEntity
{
    public int Id { get; set; }
    public int SourceTestMappingId { get; set; }
    public int StepIndex { get; set; }
    public int FromMemberId { get; set; }
    public int ToMemberId { get; set; }
    [MaxLength(100)] public string RelationshipKind { get; set; } = string.Empty;
    [MaxLength(100)] public string EdgeSource { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public virtual SourceTestMappingEntity? SourceTestMapping { get; set; }
}
