namespace TestMap.Services.TestGeneration.TargetSelection;

public sealed record MetricDrivenCandidateScore(
    int MemberId,
    double Score,
    double ExpectedMetricDelta,
    double Confidence,
    double Feasibility,
    double EstimatedCost,
    string GuardrailStatus,
    string Evidence);