using TestMap.Models.Experiment.Assertions;

namespace TestMap.Services.Experiment.Reporting;

public interface IAssertionLineageSummaryService
{
    GeneratedTestAssertionSummary ApplyTestSummary(GeneratedTestAssertionSummary summary);
    AssertionLineageMeasurement ApplyAttemptSummary(AssertionLineageMeasurement measurement);
}

public sealed class AssertionLineageSummaryService : IAssertionLineageSummaryService
{
    public GeneratedTestAssertionSummary ApplyTestSummary(GeneratedTestAssertionSummary summary)
    {
        if (summary.Status == GeneratedTestAssertionStatus.Unavailable)
        {
            ClearCounts(summary);
            return summary;
        }

        summary.RecognizedAssertionCount = summary.Observations.Count;
        summary.UnrecognizedAssertionCount ??= 0;
        summary.TracedAssertionCount =
            summary.Observations.Count(x => x.Category == AssertionLineageCategory.Traced);
        summary.TrivialAssertionCount =
            summary.Observations.Count(x => x.Category == AssertionLineageCategory.Trivial);
        summary.UnresolvedAssertionCount =
            summary.Observations.Count(x => x.Category == AssertionLineageCategory.Unresolved);
        summary.Status = summary.RecognizedAssertionCount == 0
            ? GeneratedTestAssertionStatus.NoRecognizedAssertions
            : GeneratedTestAssertionStatus.Classified;
        return summary;
    }

    public AssertionLineageMeasurement ApplyAttemptSummary(AssertionLineageMeasurement measurement)
    {
        foreach (var summary in measurement.TestSummaries)
            ApplyTestSummary(summary);

        if (measurement.Status is AssertionMeasurementStatus.NotApplicable or
            AssertionMeasurementStatus.NotMeasured)
        {
            ClearCounts(measurement);
            return measurement;
        }

        var unavailable = measurement.TestSummaries
            .Count(x => x.Status == GeneratedTestAssertionStatus.Unavailable);
        var available = measurement.TestSummaries.Count - unavailable;
        measurement.EligibleTestCount = measurement.TestSummaries.Count;
        measurement.AnalyzedTestCount = available;
        measurement.UnavailableTestCount = unavailable;
        measurement.NoRecognizedAssertionTestCount = measurement.TestSummaries
            .Count(x => x.Status == GeneratedTestAssertionStatus.NoRecognizedAssertions);

        if (measurement.TestSummaries.Count == 0 || available == 0)
        {
            measurement.Status = AssertionMeasurementStatus.Unavailable;
            measurement.FailureCode ??= AssertionLineageReasonCodes.GeneratedTestMemberUnresolved;
            measurement.FailureReason ??= measurement.TestSummaries.FirstOrDefault()?.StatusReason
                ?? "No generated test member had available assertion-lineage evidence.";
            ClearCounts(measurement);
            return measurement;
        }

        measurement.Status = unavailable > 0
            ? AssertionMeasurementStatus.Partial
            : AssertionMeasurementStatus.Complete;
        if (measurement.Status == AssertionMeasurementStatus.Partial)
        {
            measurement.FailureCode ??= AssertionLineageReasonCodes.AnalysisFailure;
            measurement.FailureReason ??= measurement.TestSummaries
                .First(x => x.Status == GeneratedTestAssertionStatus.Unavailable)
                .StatusReason
                ?? "At least one generated test has unavailable assertion-lineage evidence.";
        }
        measurement.RecognizedAssertionCount = Sum(measurement.TestSummaries, x => x.RecognizedAssertionCount);
        measurement.UnrecognizedAssertionCount = Sum(measurement.TestSummaries, x => x.UnrecognizedAssertionCount);
        measurement.TracedAssertionCount = Sum(measurement.TestSummaries, x => x.TracedAssertionCount);
        measurement.TrivialAssertionCount = Sum(measurement.TestSummaries, x => x.TrivialAssertionCount);
        measurement.UnresolvedAssertionCount = Sum(measurement.TestSummaries, x => x.UnresolvedAssertionCount);
        return measurement;
    }

    private static int Sum(
        IEnumerable<GeneratedTestAssertionSummary> summaries,
        Func<GeneratedTestAssertionSummary, int?> selector) =>
        summaries.Where(x => x.Status != GeneratedTestAssertionStatus.Unavailable)
            .Sum(x => selector(x) ?? 0);

    private static void ClearCounts(GeneratedTestAssertionSummary summary)
    {
        summary.RecognizedAssertionCount = null;
        summary.UnrecognizedAssertionCount = null;
        summary.TracedAssertionCount = null;
        summary.TrivialAssertionCount = null;
        summary.UnresolvedAssertionCount = null;
    }

    private static void ClearCounts(AssertionLineageMeasurement measurement)
    {
        measurement.EligibleTestCount = null;
        measurement.AnalyzedTestCount = null;
        measurement.UnavailableTestCount = null;
        measurement.NoRecognizedAssertionTestCount = null;
        ClearCategoryCounts(measurement);
    }

    private static void ClearCategoryCounts(AssertionLineageMeasurement measurement)
    {
        measurement.RecognizedAssertionCount = null;
        measurement.UnrecognizedAssertionCount = null;
        measurement.TracedAssertionCount = null;
        measurement.TrivialAssertionCount = null;
        measurement.UnresolvedAssertionCount = null;
    }
}
