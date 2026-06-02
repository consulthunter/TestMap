using System.ComponentModel.DataAnnotations;
using TestMap.Persistence.Ef.Entities.Rules;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class GenerationAttemptEntity
{
    public int Id { get; set; }
    public int CandidateMethodId { get; set; }
    public int? ExperimentMatrixWorkItemId { get; set; }
    [MaxLength(100)] public string ProviderName { get; set; } = string.Empty;
    [MaxLength(100)] public string ModelName { get; set; } = string.Empty;
    [MaxLength(50)] public string Strategy { get; set; } = string.Empty;
    [MaxLength(100)] public string Objective { get; set; } = string.Empty;
    [MaxLength(100)] public string GenerationApproach { get; set; } = string.Empty;
    [MaxLength(100)] public string MetricsPath { get; set; } = string.Empty;
    [MaxLength(100)] public string ContextMode { get; set; } = string.Empty;
    [MaxLength(100)] public string BudgetMode { get; set; } = string.Empty;
    [MaxLength(200)] public string AblationVariantId { get; set; } = string.Empty;
    public string StepConfigJson { get; set; } = string.Empty;
    public string EffectiveProfileJson { get; set; } = string.Empty;
    [MaxLength(64)] public string EffectiveProfileHash { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int AttemptNumber { get; set; }
    public bool IsRepairAttempt { get; set; }
    public int? ParentAttemptId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TotalTokensUsed { get; set; }
    [MaxLength(50)] public string Status { get; set; } = string.Empty;
    [MaxLength(50)] public string FailureKind { get; set; } = string.Empty;
    [MaxLength(50)] public string FailureStage { get; set; } = string.Empty;
    [MaxLength(100)] public string FailureCategory { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string RuleDecisionSnapshotJson { get; set; } = string.Empty;

    public virtual CandidateMethodEntity? CandidateMethod { get; set; }
    public virtual ExperimentMatrixWorkItemEntity? ExperimentMatrixWorkItem { get; set; }
    public virtual GenerationAttemptEntity? ParentAttempt { get; set; }
    public virtual ICollection<GenerationStepEntity> GenerationSteps { get; set; } = new List<GenerationStepEntity>();
    public virtual GeneratedTestExecutionEntity? TestExecution { get; set; }
    public virtual ICollection<RuleDecisionEntity> RuleDecisions { get; set; } = new List<RuleDecisionEntity>();
}
