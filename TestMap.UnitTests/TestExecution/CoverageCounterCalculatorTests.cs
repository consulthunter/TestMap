using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.UnitTests.TestExecution;

public sealed class CoverageCounterCalculatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_DistinctLinesAndPositiveHits_AreCountedExactlyOnce()
    {
        var result = CoverageCounterCalculator.Calculate(
        [
            new LineCoverageModel { Number = 10, Hits = 0 },
            new LineCoverageModel { Number = 10, Hits = 2 },
            new LineCoverageModel { Number = 11, Hits = 0 }
        ]);

        Assert.Equal(1, result.LinesCovered);
        Assert.Equal(2, result.LinesValid);
        Assert.True(result.LineCountsAvailable);
        Assert.Equal(0, result.BranchesValid);
        Assert.True(result.BranchCountsAvailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ConditionFractionsAndDuplicateDetails_UseOneBestDetailPerLine()
    {
        var result = CoverageCounterCalculator.Calculate(
        [
            new LineCoverageModel { Number = 20, Hits = 1, Branch = "true", ConditionCoverage = "50% (1/2)" },
            new LineCoverageModel { Number = 20, Hits = 1, Branch = "true", ConditionCoverage = "50% (1/2)" },
            new LineCoverageModel { Number = 21, Hits = 1, Branch = "true", ConditionCoverage = "75% (3/4)" }
        ]);

        Assert.Equal(4, result.BranchesCovered);
        Assert.Equal(6, result.BranchesValid);
        Assert.True(result.BranchCountsAvailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_MalformedFraction_UsesChildConditionFallback()
    {
        var result = CoverageCounterCalculator.Calculate(
        [
            new LineCoverageModel
            {
                Number = 30,
                Hits = 1,
                Branch = "true",
                ConditionCoverage = "unknown",
                Conditions =
                [
                    new ConditionCoverageModel { Coverage = "100%" },
                    new ConditionCoverageModel { Coverage = "0%" }
                ]
            }
        ]);

        Assert.Equal(1, result.BranchesCovered);
        Assert.Equal(2, result.BranchesValid);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_MalformedBranchWithoutChildren_MarksBranchesUnavailable()
    {
        var result = CoverageCounterCalculator.Calculate(
        [new LineCoverageModel { Number = 40, Hits = 0, Branch = "true", ConditionCoverage = "broken" }]);

        Assert.True(result.LineCountsAvailable);
        Assert.False(result.BranchCountsAvailable);
        Assert.Equal(0, result.BranchesValid);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_CoveredGreaterThanValid_IsRejected()
    {
        var result = CoverageCounterCalculator.Calculate(
        [new LineCoverageModel { Number = 50, Hits = 1, Branch = "true", ConditionCoverage = "150% (3/2)" }]);

        Assert.False(result.IsValid);
        Assert.Contains("exceeds", result.ValidationError);
        Assert.False(result.BranchCountsAvailable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_NoLineDetails_IsUnavailableRatherThanObservedZero()
    {
        var result = CoverageCounterCalculator.Calculate([]);

        Assert.False(result.LineCountsAvailable);
        Assert.False(result.BranchCountsAvailable);
    }
}
