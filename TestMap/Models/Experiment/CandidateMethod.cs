using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Models.Experiment;

/// <summary>
/// Represents a source method selected as a candidate for test generation.
/// </summary>
public class CandidateMethod
{
    public int Id { get; set; }
    public int ExperimentRunId { get; set; }
    public int MemberId { get; set; }
    public int? ExistingTestMemberId { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? ExistingTestMethodName { get; set; }
    public double BaselineCoverage { get; set; }
    public double ComplexityScore { get; set; }
    public double? RiskScore { get; set; }
    public Dictionary<RiskFactorKind, double> RiskFactorScores { get; set; } = new();
    public Dictionary<RiskFactorKind, double> RiskWeights { get; set; } = new();
    public string RiskSelectionReason { get; set; } = string.Empty;
    public double? MetricDrivenScore { get; set; }
    public double? ExpectedMetricDelta { get; set; }
    public double? MetricConfidence { get; set; }
    public double? MetricFeasibility { get; set; }
    public double? MetricEstimatedCost { get; set; }
    public string MetricGuardrailStatus { get; set; } = string.Empty;
    public string MetricSelectionReason { get; set; } = string.Empty;
    public double? TestImprovementScore { get; set; }
    public string TestImprovementReason { get; set; } = string.Empty;
    public CandidateTestState TestState { get; set; } = CandidateTestState.Unknown;
    public CandidateActionKind RecommendedAction { get; set; } = CandidateActionKind.None;
    public string TestStateReason { get; set; } = string.Empty;
    public DateTime SelectionTime { get; set; }

    public virtual ExperimentRun? ExperimentRun { get; set; }
    public virtual ICollection<GenerationAttempt> GenerationAttempts { get; set; } = new List<GenerationAttempt>();
}