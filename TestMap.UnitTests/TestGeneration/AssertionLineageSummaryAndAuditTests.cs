using TestMap.Models.Experiment.Assertions;
using TestMap.Services.Experiment.Reporting;

namespace TestMap.UnitTests.TestGeneration;

public sealed class AssertionLineageSummaryAndAuditTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyAttemptSummary_AggregatesClassifiedAndNoAssertionTests()
    {
        var measurement = MakeMeasurement();
        measurement.TestSummaries.Add(new GeneratedTestAssertionSummary
        {
            TestMemberId = 101,
            Observations =
            [
                MakeObservation(0, AssertionLineageCategory.Traced),
                MakeObservation(1, AssertionLineageCategory.Trivial),
                MakeObservation(2, AssertionLineageCategory.Unresolved)
            ]
        });
        measurement.TestSummaries.Add(new GeneratedTestAssertionSummary
        {
            TestMemberId = 102,
            UnrecognizedAssertionCount = 1
        });

        new AssertionLineageSummaryService().ApplyAttemptSummary(measurement);

        Assert.Equal(AssertionMeasurementStatus.Complete, measurement.Status);
        Assert.Equal(2, measurement.EligibleTestCount);
        Assert.Equal(2, measurement.AnalyzedTestCount);
        Assert.Equal(0, measurement.UnavailableTestCount);
        Assert.Equal(1, measurement.NoRecognizedAssertionTestCount);
        Assert.Equal(3, measurement.RecognizedAssertionCount);
        Assert.Equal(1, measurement.UnrecognizedAssertionCount);
        Assert.Equal(1, measurement.TracedAssertionCount);
        Assert.Equal(1, measurement.TrivialAssertionCount);
        Assert.Equal(1, measurement.UnresolvedAssertionCount);
        Assert.Equal(
            GeneratedTestAssertionStatus.NoRecognizedAssertions,
            measurement.TestSummaries[1].Status);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyAttemptSummary_WhenEveryTestIsUnavailable_LeavesAllCountsNull()
    {
        var measurement = MakeMeasurement();
        measurement.TestSummaries.Add(new GeneratedTestAssertionSummary
        {
            TestMemberId = 101,
            Status = GeneratedTestAssertionStatus.Unavailable,
            StatusReason = AssertionLineageReasonCodes.SourceUnavailable
        });

        new AssertionLineageSummaryService().ApplyAttemptSummary(measurement);

        Assert.Equal(AssertionMeasurementStatus.Unavailable, measurement.Status);
        Assert.Equal(AssertionLineageReasonCodes.GeneratedTestMemberUnresolved, measurement.FailureCode);
        Assert.Null(measurement.EligibleTestCount);
        Assert.Null(measurement.AnalyzedTestCount);
        Assert.Null(measurement.UnavailableTestCount);
        Assert.Null(measurement.NoRecognizedAssertionTestCount);
        Assert.Null(measurement.RecognizedAssertionCount);
        Assert.Null(measurement.UnrecognizedAssertionCount);
        Assert.Null(measurement.TracedAssertionCount);
        Assert.Null(measurement.TrivialAssertionCount);
        Assert.Null(measurement.UnresolvedAssertionCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateMeasurement_AcceptsProductionBackedTrace()
    {
        var measurement = MakeMeasurement();
        measurement.TestSummaries.Add(new GeneratedTestAssertionSummary
        {
            TestMemberId = 101,
            Observations = [MakeObservation(0, AssertionLineageCategory.Traced)]
        });
        new AssertionLineageSummaryService().ApplyAttemptSummary(measurement);

        var result = new AssertionLineageAuditService().ValidateMeasurement(measurement);

        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Findings));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateMeasurement_RejectsTracedAssertionWithoutProductionTerminal()
    {
        var measurement = MakeMeasurement();
        var observation = MakeObservation(0, AssertionLineageCategory.Traced);
        observation.Steps[0].StepKind = AssertionLineageStepKind.TestLocal;
        observation.Steps[0].MemberId = null;
        observation.Steps[0].Outcome = AssertionLineageStepOutcome.Traced;
        measurement.TestSummaries.Add(new GeneratedTestAssertionSummary
        {
            TestMemberId = 101,
            Observations = [observation]
        });
        new AssertionLineageSummaryService().ApplyAttemptSummary(measurement);

        var result = new AssertionLineageAuditService().ValidateMeasurement(measurement);

        Assert.Contains(result.Findings, x => x.Code == "TracedWithoutProduction");
    }

    private static AssertionLineageMeasurement MakeMeasurement() => new()
    {
        ProjectId = 1,
        ExperimentRunId = 2,
        CandidateMethodId = 3,
        GenerationAttemptId = 4,
        ProducerLane = "testmap",
        PolicyVersion = "assertion-lineage-v1",
        AssertionCatalogVersion = "assertion-catalog-v1",
        MaxDepth = 4,
        StartedAt = DateTime.UtcNow,
        CompletedAt = DateTime.UtcNow
    };

    private static AssertionObservation MakeObservation(
        int ordinal,
        AssertionLineageCategory category)
    {
        var outcome = category switch
        {
            AssertionLineageCategory.Traced => AssertionLineageStepOutcome.Traced,
            AssertionLineageCategory.Trivial => AssertionLineageStepOutcome.Trivial,
            _ => AssertionLineageStepOutcome.Unresolved
        };
        var stepKind = category switch
        {
            AssertionLineageCategory.Traced => AssertionLineageStepKind.ProductionMember,
            AssertionLineageCategory.Trivial => AssertionLineageStepKind.Literal,
            _ => AssertionLineageStepKind.DepthLimit
        };

        return new AssertionObservation
        {
            Ordinal = ordinal,
            Framework = "xunit",
            AssertionMethod = "Equal",
            RecognitionKind = AssertionRecognitionKind.Semantic,
            FilePath = "WidgetTests.cs",
            StartLine = 10 + ordinal,
            StartColumn = 1,
            EndLine = 10 + ordinal,
            EndColumn = 20,
            ExpressionHash = $"hash-{ordinal}",
            Category = category,
            ResolutionCode = category == AssertionLineageCategory.Traced
                ? AssertionLineageReasonCodes.ProductionInvocation
                : category == AssertionLineageCategory.Trivial
                    ? AssertionLineageReasonCodes.AllInputsTestLocal
                    : AssertionLineageReasonCodes.DepthExceeded,
            DepthReached = 1,
            TargetRelation = category == AssertionLineageCategory.Traced
                ? AssertionTargetRelation.Candidate
                : AssertionTargetRelation.NoProduction,
            TraceSummary = "Stable trace summary.",
            Steps =
            [
                new AssertionLineageStep
                {
                    InputIndex = 0,
                    PathIndex = 0,
                    StepIndex = 0,
                    StepKind = stepKind,
                    Depth = 1,
                    SymbolDisplay = category == AssertionLineageCategory.Traced
                        ? "Widget.Calculate()"
                        : "literal",
                    MemberId = category == AssertionLineageCategory.Traced ? 3 : null,
                    Outcome = outcome,
                    ReasonCode = category == AssertionLineageCategory.Traced
                        ? AssertionLineageReasonCodes.ProductionInvocation
                        : category == AssertionLineageCategory.Trivial
                            ? AssertionLineageReasonCodes.AllInputsTestLocal
                            : AssertionLineageReasonCodes.DepthExceeded,
                    Summary = "Terminal lineage step."
                }
            ]
        };
    }
}
