using TestMap.Models.Experiment.Assertions;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.UnitTests.StaticAnalysis.Assertions;

public sealed class AssertionLineageClassifierTests
{
    private readonly AssertionLineageClassifier _classifier = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Components_AnyTraced_IsTraced()
    {
        var result = _classifier.CombineComponents(
        [
            AssertionSliceResult.Trivial(AssertionLineageReasonCodes.AllInputsTestLocal, 0),
            new AssertionSliceResult(
                AssertionLineageCategory.Traced,
                AssertionLineageReasonCodes.ProductionInvocation,
                1,
                [7],
                [])
        ]);

        Assert.Equal(AssertionLineageCategory.Traced, result.Category);
        Assert.Equal([7], result.ProductionMemberIds);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Alternatives_MixedTracedAndTrivial_IsUnresolved()
    {
        var result = _classifier.CombineAlternatives(
        [
            AssertionSliceResult.Trivial(AssertionLineageReasonCodes.AllInputsTestLocal, 0),
            new AssertionSliceResult(
                AssertionLineageCategory.Traced,
                AssertionLineageReasonCodes.ProductionInvocation,
                1,
                [7],
                [])
        ]);

        Assert.Equal(AssertionLineageCategory.Unresolved, result.Category);
        Assert.Equal(AssertionLineageReasonCodes.AmbiguousDefinitions, result.ReasonCode);
    }
}
