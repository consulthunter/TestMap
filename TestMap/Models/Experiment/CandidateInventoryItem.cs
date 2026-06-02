using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Models.Experiment;

public class CandidateInventoryItem
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int SourceMemberId { get; set; }
    public int? SourceTestMappingId { get; set; }
    public int? ExistingTestMemberId { get; set; }
    public string SourceMethodName { get; set; } = string.Empty;
    public string SourceMethodSignature { get; set; } = string.Empty;
    public string ExistingTestMethodName { get; set; } = string.Empty;
    public double InitialCoverage { get; set; }
    public double ComplexityScore { get; set; }
    public TargetSelectionStrategy SelectionStrategy { get; set; } = TargetSelectionStrategy.Existing;
    public string ExistingTestOutcome { get; set; } = string.Empty;
    public bool IsExperimentEligible { get; set; }
    public string IneligibilityReason { get; set; } = string.Empty;
    public double? RiskScore { get; set; }
    public double? MetricDrivenScore { get; set; }
    public double? ExpectedMetricDelta { get; set; }
    public string MetricGuardrailStatus { get; set; } = string.Empty;
    public string MetricSelectionReason { get; set; } = string.Empty;
    public CandidateTestState TestState { get; set; } = CandidateTestState.Unknown;
    public CandidateActionKind RecommendedAction { get; set; } = CandidateActionKind.None;
    public string TestStateReason { get; set; } = string.Empty;
    public DateTime SelectionTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public string BaselineRunId { get; set; } = string.Empty;
    public string ContextEvidenceKind { get; set; } = "None";
    public string ContextEvidenceSummary { get; set; } = string.Empty;
    public bool HasGroundedTestContext { get; set; }
    public int? MappedTestMemberId { get; set; }
    public string MappedTestMethodName { get; set; } = string.Empty;
    public string AccessPathStrategy { get; set; } = string.Empty;
    public string AccessPathMemberIdsJson { get; set; } = "[]";
    public string TraceSummary { get; set; } = string.Empty;
    public string TraceJson { get; set; } = "{}";
    public string TestIntentionsSummary { get; set; } = string.Empty;
    public string TypeConstructionSummary { get; set; } = string.Empty;
    public string CandidateMetadataJson { get; set; } = "{}";
}
