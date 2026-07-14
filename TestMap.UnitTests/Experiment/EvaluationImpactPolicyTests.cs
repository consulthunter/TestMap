using TestMap.Models.Experiment;

namespace TestMap.UnitTests.Experiment;

public sealed class EvaluationImpactPolicyTests
{
    [Theory]
    [InlineData(0.01, null, true)]
    [InlineData(0.009999, null, false)]
    [InlineData(null, 1.0, true)]
    [InlineData(null, 0.9999, false)]
    [Trait("Category", "Unit")]
    public void Evaluate_UsesDeclaredNoiseFloors(double? coverageDelta, double? mutationDelta, bool improved)
    {
        var result = EvaluationImpactPolicy.Evaluate(true, coverageDelta, mutationDelta);

        Assert.Equal(improved, result.MetricImproved);
        Assert.Equal(improved, result.ValidatedEvidencePositive);
        Assert.Equal(!improved, result.ValidatedLowImpact);
        Assert.Equal(improved, result.PositiveImpact);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_PassingAttemptWithNoMeasurements_HasUnknownImpact()
    {
        var result = EvaluationImpactPolicy.Evaluate(true, null, null);

        Assert.True(result.ValidatedSuccess);
        Assert.False(result.ImpactEvaluable);
        Assert.False(result.ValidatedEvidencePositive);
        Assert.False(result.ValidatedLowImpact);
        Assert.Null(result.PositiveImpact);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_LowImpactPassingAttempt_IsSuccessWithoutPositiveImpact()
    {
        var result = EvaluationImpactPolicy.Evaluate(true, 0.005, -2.0);

        Assert.True(result.ValidatedSuccess);
        Assert.True(result.ValidatedLowImpact);
        Assert.False(result.PositiveImpact);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_FailedAttempt_CanImproveMetricsWithoutBeingPositiveImpact()
    {
        var result = EvaluationImpactPolicy.Evaluate(false, 0.2, null);

        Assert.True(result.MetricImproved);
        Assert.False(result.ValidatedEvidencePositive);
        Assert.False(result.PositiveImpact);
    }
}
