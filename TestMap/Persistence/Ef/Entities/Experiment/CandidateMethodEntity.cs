using System.ComponentModel.DataAnnotations;
using TestMap.Persistence.Ef.Entities.Rules;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class CandidateMethodEntity
{
    public int Id { get; set; }
    public int ExperimentRunId { get; set; }
    public int? CandidateInventoryId { get; set; }
    public int SourceMemberId { get; set; }
    public int? ExistingTestMemberId { get; set; }
    [MaxLength(500)] public string SourceMethodName { get; set; } = string.Empty;
    [MaxLength(2000)] public string SourceMethodSignature { get; set; } = string.Empty;
    [MaxLength(500)] public string ExistingTestMethodName { get; set; } = string.Empty;
    public double InitialCoverage { get; set; }
    public int InitialCoveredLines { get; set; }
    public int InitialTotalLines { get; set; }
    public double? MetricDrivenScore { get; set; }
    public double? ExpectedMetricDelta { get; set; }
    public double? MetricConfidence { get; set; }
    public double? MetricFeasibility { get; set; }
    public double? MetricEstimatedCost { get; set; }
    [MaxLength(50)] public string MetricGuardrailStatus { get; set; } = string.Empty;
    public string MetricSelectionReason { get; set; } = string.Empty;
    public double? TestImprovementScore { get; set; }
    public string TestImprovementReason { get; set; } = string.Empty;
    [MaxLength(50)] public string TestState { get; set; } = string.Empty;
    [MaxLength(50)] public string RecommendedAction { get; set; } = string.Empty;
    public string TestStateReason { get; set; } = string.Empty;
    public string TestIntentionsSummary { get; set; } = string.Empty;
    public string TypeConstructionSummary { get; set; } = string.Empty;
    public string CandidateMetadataJson { get; set; } = "{}";
    public DateTime SelectionTime { get; set; }

    public virtual ExperimentRunEntity? ExperimentRun { get; set; }
    public virtual CandidateInventoryEntity? CandidateInventory { get; set; }

    public virtual ICollection<GenerationAttemptEntity> GenerationAttempts { get; set; } =
        new List<GenerationAttemptEntity>();

    public virtual ICollection<ExperimentMatrixWorkItemEntity> ExperimentMatrixWorkItems { get; set; } =
        new List<ExperimentMatrixWorkItemEntity>();

    public virtual ICollection<RuleDecisionEntity> RuleDecisions { get; set; } =
        new List<RuleDecisionEntity>();
}
