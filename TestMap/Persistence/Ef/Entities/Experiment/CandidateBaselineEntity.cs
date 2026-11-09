using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public sealed class CandidateBaselineEntity
{
    public int Id { get; set; }
    [MaxLength(500)] public string StableKey { get; set; } = string.Empty;
    public int ExperimentRunId { get; set; }
    public int CandidateMethodId { get; set; }
    public int MemberId { get; set; }
    public int? TestRunId { get; set; }
    public bool Usable { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
