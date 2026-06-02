namespace TestMap.Models.Experiment;

public class SourceTestMappingItem
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int SourceMemberId { get; set; }
    public int TestMemberId { get; set; }
    public string EvidenceKind { get; set; } = string.Empty;
    public bool IsGrounded { get; set; }
    public string AccessPathStrategy { get; set; } = string.Empty;
    public int PathLength { get; set; }
    public double Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string ResolverVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<SourceTestMappingTraceStepItem> TraceSteps { get; set; } = new();
}
