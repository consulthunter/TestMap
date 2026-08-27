using TestMap.Models.Experiment.Assertions;
using TestMap.Services.Experiment.Reporting;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.UnitTests.StaticAnalysis.Assertions;

/// <summary>
/// Analyzer output must survive the integrity audit that runs during persistence. Nothing
/// else covers that seam: the analyzer tests stop at the classification, and the audit tests
/// build their measurements by hand, so an observation the analyzer can produce but the audit
/// rejects reaches a real experiment run before anyone sees it.
/// </summary>
public sealed class AssertionLineageAuditReproTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("bound terminal", "Xunit.Assert.Equal(42, new Product.Service().GetValue());")]
    [InlineData("unbound terminal", "CustomAssert.IsTrue(new Product.Service().GetValue() == 42);")]
    [InlineData("unbound with constraint", "CustomAssert.That(new Product.Service().GetValue(), Is.EqualTo(42));")]
    [InlineData("unbound literal only", "CustomAssert.IsTrue(1 == 1);")]
    [InlineData("bound literal only", "Xunit.Assert.True(true);")]
    [InlineData("bound multi-hop", "var v = new Product.Service().GetValue(); Xunit.Assert.Equal(42, v);")]
    public async Task AnalyzerOutput_PassesIntegrityAudit(string label, string body)
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(body);
        var result = await harness.AnalyzeAsync();
        Assert.True(result.Available, result.FailureReason);

        var measurement = new AssertionLineageMeasurement
        {
            ProjectId = 1,
            ExperimentRunId = 1,
            CandidateMethodId = 1,
            GenerationAttemptId = 1,
            ProducerLane = "testmap",
            Status = AssertionMeasurementStatus.Complete,
            PolicyVersion = AssertionLineagePolicy.CurrentPolicyVersion,
            AssertionCatalogVersion = AssertionLineagePolicy.CurrentCatalogVersion,
            MaxDepth = new AssertionLineagePolicy().MaxDepth,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        foreach (var summary in result.TestSummaries)
            measurement.TestSummaries.Add(summary);

        new AssertionLineageSummaryService().ApplyAttemptSummary(measurement);
        var audit = new AssertionLineageAuditService().ValidateMeasurement(measurement);

        Assert.True(
            audit.Findings.Count == 0,
            $"[{label}] audit findings: " +
            string.Join(" | ", audit.Findings.Select(x => $"{x.Code}: {x.Message}")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UnboundAssertionTerminal_TracesOperandToProductionAndPassesAudit()
    {
        // 'CustomAssert' is declared nowhere, so the terminal has no symbol — the same
        // situation as a test framework arriving from a package the compilation lacks. The
        // operand still binds through the project reference and must stay traceable.
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            "CustomAssert.IsTrue(new Product.Service().GetValue() == 42);");

        var result = await harness.AnalyzeAsync();
        var summary = Assert.Single(result.TestSummaries);
        var observation = Assert.Single(summary.Observations);

        Assert.Equal(AssertionRecognitionKind.SyntacticFallback, observation.RecognitionKind);
        Assert.Equal(AssertionLineageCategory.Traced, observation.Category);
        Assert.Contains(observation.Steps, step =>
            step.StepKind == AssertionLineageStepKind.ProductionMember &&
            step.MemberId == harness.IntendedSourceMemberId);
    }
}
