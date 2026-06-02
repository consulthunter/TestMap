using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class SourceTestMappingEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int SourceMemberId { get; set; }
    public int TestMemberId { get; set; }
    [MaxLength(100)] public string EvidenceKind { get; set; } = string.Empty;
    public bool IsGrounded { get; set; }
    [MaxLength(100)] public string AccessPathStrategy { get; set; } = string.Empty;
    public int PathLength { get; set; }
    public double Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
    [MaxLength(50)] public string ResolverVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public virtual ICollection<SourceTestMappingTraceStepEntity> TraceSteps { get; set; } =
        new List<SourceTestMappingTraceStepEntity>();
}
