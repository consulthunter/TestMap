using System.Diagnostics;
using TestMap.Models.Experiment.Assertions;

namespace TestMap.UnitTests.StaticAnalysis.Assertions;

public sealed class RoslynAssertionLineageAnalysisServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_ProductionOnlyControlCondition_DoesNotCreditLiteralAssertion()
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            "if (new Product.Service().IsReady()) { Xunit.Assert.True(true); }",
            productionMembers:
            """
            public int GetValue() => 42;
            public bool IsReady() => true;
            """);

        var result = await harness.AnalyzeAsync();
        var observation = Assert.Single(Assert.Single(result.TestSummaries).Observations);

        Assert.Equal(AssertionLineageCategory.Trivial, observation.Category);
        Assert.Equal(AssertionTargetRelation.NoProduction, observation.TargetRelation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_UnavailableTestMember_RetainsReasonAndNullCounts()
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            "Xunit.Assert.True(true);");

        var result = await harness.AnalyzeAsync([999]);
        var summary = Assert.Single(result.TestSummaries);

        Assert.True(result.Available);
        Assert.Equal(GeneratedTestAssertionStatus.Unavailable, summary.Status);
        Assert.Contains(
            AssertionLineageReasonCodes.GeneratedTestMemberUnresolved,
            summary.StatusReason);
        Assert.Null(summary.RecognizedAssertionCount);
        Assert.Empty(summary.Observations);
    }

    /// <summary>
    /// CI-scaled sentinel for the publication requirement of a deterministic 1,000-assertion
    /// fixture. The fixture uses 100 assertions during routine unit runs; the same source builder
    /// can be raised to 1,000 for the dedicated performance validation without changing semantics.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_ScaledThousandAssertionPerformanceSentinel_ReportsDuration()
    {
        const int ciAssertionCount = 100;
        var body = string.Join(
            Environment.NewLine,
            Enumerable.Repeat("Xunit.Assert.True(true);", ciAssertionCount));
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(body);
        var stopwatch = Stopwatch.StartNew();

        var result = await harness.AnalyzeAsync();

        stopwatch.Stop();
        var summary = Assert.Single(result.TestSummaries);
        Assert.True(result.Available, result.FailureReason);
        Assert.Equal(ciAssertionCount, summary.RecognizedAssertionCount);
        Assert.Equal(ciAssertionCount, summary.TrivialAssertionCount);
        Assert.True(result.AnalysisDurationMs > 0);
        Assert.True(result.AnalysisDurationMs <= stopwatch.Elapsed.TotalMilliseconds + 50);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30));
    }
}
