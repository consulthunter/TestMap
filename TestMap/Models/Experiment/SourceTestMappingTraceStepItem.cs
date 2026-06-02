namespace TestMap.Models.Experiment;

public class SourceTestMappingTraceStepItem
{
    public int Id { get; set; }
    public int SourceTestMappingId { get; set; }
    public int StepIndex { get; set; }
    public int FromMemberId { get; set; }
    public int ToMemberId { get; set; }
    public string RelationshipKind { get; set; } = string.Empty;
    public string EdgeSource { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
