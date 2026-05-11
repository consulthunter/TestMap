using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class CandidateInventoryEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int SourceMemberId { get; set; }
    public int? ExistingTestMemberId { get; set; }
    [MaxLength(500)] public string SourceMethodName { get; set; } = string.Empty;
    [MaxLength(2000)] public string SourceMethodSignature { get; set; } = string.Empty;
    [MaxLength(500)] public string ExistingTestMethodName { get; set; } = string.Empty;
    public double InitialCoverage { get; set; }
    public double ComplexityScore { get; set; }
    [MaxLength(100)] public string SelectionStrategy { get; set; } = string.Empty;
    [MaxLength(100)] public string ExistingTestOutcome { get; set; } = string.Empty;
    public bool IsExperimentEligible { get; set; }
    public string IneligibilityReason { get; set; } = string.Empty;
    public double? RiskScore { get; set; }
    public double? MetricDrivenScore { get; set; }
    public double? ExpectedMetricDelta { get; set; }
    [MaxLength(50)] public string MetricGuardrailStatus { get; set; } = string.Empty;
    public string MetricSelectionReason { get; set; } = string.Empty;
    [MaxLength(50)] public string TestState { get; set; } = string.Empty;
    [MaxLength(50)] public string RecommendedAction { get; set; } = string.Empty;
    public string TestStateReason { get; set; } = string.Empty;
    public DateTime SelectionTime { get; set; }
    [MaxLength(100)] public string BaselineRunId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
