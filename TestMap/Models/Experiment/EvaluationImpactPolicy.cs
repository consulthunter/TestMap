namespace TestMap.Models.Experiment;

public static class EvaluationImpactPolicy
{
    public const string Version = "1.0";
    public const double CoverageNoiseFloor = 0.01;
    public const double MutationNoiseFloor = 1.0;

    public static EvaluationImpactDecision Evaluate(
        bool validatedSuccess,
        double? coverageDelta,
        double? mutationScoreDelta)
    {
        var coverageEvaluable = coverageDelta.HasValue;
        var mutationEvaluable = mutationScoreDelta.HasValue;
        var impactEvaluable = coverageEvaluable || mutationEvaluable;
        var coverageImproved = coverageDelta is >= CoverageNoiseFloor;
        var mutationImproved = mutationScoreDelta is >= MutationNoiseFloor;
        var metricImproved = coverageImproved || mutationImproved;
        var evidencePositive = validatedSuccess && metricImproved;

        return new EvaluationImpactDecision(
            validatedSuccess,
            coverageEvaluable,
            mutationEvaluable,
            impactEvaluable,
            coverageImproved,
            mutationImproved,
            metricImproved,
            evidencePositive,
            validatedSuccess && impactEvaluable && !metricImproved,
            evidencePositive ? true : impactEvaluable ? false : null);
    }
}

public sealed record EvaluationImpactDecision(
    bool ValidatedSuccess,
    bool CoverageEvaluable,
    bool MutationEvaluable,
    bool ImpactEvaluable,
    bool CoverageImproved,
    bool MutationImproved,
    bool MetricImproved,
    bool ValidatedEvidencePositive,
    bool ValidatedLowImpact,
    bool? PositiveImpact);
