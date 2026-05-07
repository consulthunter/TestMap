using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class ExperimentMatrixWorkItemEntity
{
    public int Id { get; set; }
    public int ExperimentRunId { get; set; }
    public int CandidateMethodId { get; set; }
    public int MemberId { get; set; }
    [MaxLength(500)] public string StableKey { get; set; } = string.Empty;
    [MaxLength(50)] public string Status { get; set; } = string.Empty;
    [MaxLength(100)] public string ProviderName { get; set; } = string.Empty;
    [MaxLength(100)] public string ModelName { get; set; } = string.Empty;
    [MaxLength(100)] public string Objective { get; set; } = string.Empty;
    [MaxLength(100)] public string Approach { get; set; } = string.Empty;
    [MaxLength(100)] public string MetricsPath { get; set; } = string.Empty;
    [MaxLength(100)] public string ContextMode { get; set; } = string.Empty;
    [MaxLength(100)] public string BudgetMode { get; set; } = string.Empty;
    [MaxLength(200)] public string AblationVariantId { get; set; } = string.Empty;
    public string StepConfigJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public virtual ExperimentRunEntity? ExperimentRun { get; set; }
    public virtual CandidateMethodEntity? CandidateMethod { get; set; }
}
