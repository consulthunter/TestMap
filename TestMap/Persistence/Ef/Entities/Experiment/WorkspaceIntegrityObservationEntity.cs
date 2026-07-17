using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public sealed class WorkspaceIntegrityObservationEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? ExperimentRunId { get; set; }
    [MaxLength(64)] public string TargetId { get; set; } = string.Empty;
    [MaxLength(50)] public string? ProducerLane { get; set; }
    [MaxLength(512)] public string? WorkItemStableKey { get; set; }
    public int? AttemptNumber { get; set; }
    [MaxLength(50)] public string Checkpoint { get; set; } = string.Empty;
    [MaxLength(40)] public string ExpectedCommit { get; set; } = string.Empty;
    [MaxLength(40)] public string? ActualCommit { get; set; }
    public bool OriginMatches { get; set; }
    public bool? WorkingTreeDirty { get; set; }
    [MaxLength(50)] public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime ObservedAtUtc { get; set; }
}
