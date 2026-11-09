using System.Reflection;
using TestMap.Models.Experiment.Assertions;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.Services.Experiment.Reporting;

public sealed record AssertionLineageAuditFinding(string Code, string Message);

public sealed class AssertionLineageAuditResult
{
    public IReadOnlyList<AssertionLineageAuditFinding> Findings { get; init; } = [];
    public bool Passed => Findings.Count == 0;

    public void ThrowIfFailed()
    {
        if (!Passed)
            throw new InvalidOperationException(
                "Assertion-lineage integrity audit failed: " +
                string.Join("; ", Findings.Select(x => $"{x.Code}: {x.Message}")));
    }
}

public interface IAssertionLineageAuditService
{
    AssertionLineageAuditResult ValidateMeasurement(AssertionLineageMeasurement measurement);

    AssertionLineageAuditResult ValidatePublication(
        IReadOnlyCollection<AssertionLineageMeasurement> measurements,
        IReadOnlyCollection<AssertionObservationFileRow> sidecarRows);
}

public sealed class AssertionLineageAuditService : IAssertionLineageAuditService
{
    private static readonly HashSet<string> StableReasonCodes = typeof(AssertionLineageReasonCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(x => x.IsLiteral && !x.IsInitOnly && x.FieldType == typeof(string))
        .Select(x => (string)x.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    internal static bool IsStableReasonCode(string? value) =>
        value != null && StableReasonCodes.Contains(value);

    public AssertionLineageAuditResult ValidateMeasurement(AssertionLineageMeasurement measurement)
    {
        var findings = new List<AssertionLineageAuditFinding>();
        ValidateMeasurement(measurement, findings);
        return new AssertionLineageAuditResult { Findings = findings };
    }

    public AssertionLineageAuditResult ValidatePublication(
        IReadOnlyCollection<AssertionLineageMeasurement> measurements,
        IReadOnlyCollection<AssertionObservationFileRow> sidecarRows)
    {
        var findings = new List<AssertionLineageAuditFinding>();
        foreach (var measurement in measurements)
            ValidateMeasurement(measurement, findings);

        foreach (var group in measurements.GroupBy(x => new { x.ExperimentRunId, x.CandidateMethodId }))
        {
            var policies = group.Select(x =>
                    (x.PolicyVersion, x.AssertionCatalogVersion, x.MaxDepth))
                .Distinct()
                .ToList();
            if (policies.Count > 1)
                Add(findings, "CrossLanePolicyMismatch",
                    $"Candidate {group.Key.CandidateMethodId} uses different assertion policies across lanes.");
        }

        var observations = measurements
            .SelectMany(x => x.TestSummaries)
            .SelectMany(x => x.Observations)
            .ToList();
        var duplicateSidecarIds = sidecarRows.GroupBy(x => x.ObservationId).Where(x => x.Count() > 1).ToList();
        if (duplicateSidecarIds.Count > 0)
            Add(findings, "DuplicateSidecarObservation",
                $"{duplicateSidecarIds.Count} assertion observation IDs are duplicated in the sidecar.");

        var sidecarById = sidecarRows
            .GroupBy(x => x.ObservationId)
            .ToDictionary(x => x.Key, x => x.First());
        foreach (var observation in observations)
        {
            if (observation.Id <= 0 || !sidecarById.TryGetValue(observation.Id, out var row))
            {
                Add(findings, "MissingSidecarObservation",
                    $"Observation {observation.Id} is missing from the assertion sidecar.");
                continue;
            }

            var measurement = measurements.First(x =>
                x.TestSummaries.Any(summary => summary.Observations.Contains(observation)));
            if (!string.Equals(row.Category, observation.Category.ToString(), StringComparison.Ordinal) ||
                !string.Equals(row.ResolutionCode, observation.ResolutionCode, StringComparison.Ordinal) ||
                !string.Equals(row.PolicyVersion, measurement.PolicyVersion, StringComparison.Ordinal) ||
                !string.Equals(
                    row.AssertionCatalogVersion,
                    measurement.AssertionCatalogVersion,
                    StringComparison.Ordinal) ||
                row.MaxDepth != measurement.MaxDepth)
                Add(findings, "SidecarObservationMismatch",
                    $"Observation {observation.Id} does not match its persisted classification or policy.");
        }

        var persistedIds = observations.Select(x => x.Id).ToHashSet();
        foreach (var orphan in sidecarRows.Where(x => !persistedIds.Contains(x.ObservationId)))
            Add(findings, "OrphanSidecarObservation",
                $"Sidecar observation {orphan.ObservationId} has no persisted observation.");

        return new AssertionLineageAuditResult { Findings = findings };
    }

    private static void ValidateMeasurement(
        AssertionLineageMeasurement measurement,
        ICollection<AssertionLineageAuditFinding> findings)
    {
        if (measurement.ProjectId <= 0 ||
            measurement.ExperimentRunId <= 0 ||
            measurement.CandidateMethodId <= 0)
            Add(findings, "MissingOwner", "Measurement requires project, experiment, and candidate ownership.");
        if (measurement.GenerationAttemptId.HasValue == measurement.ToolAttemptId.HasValue)
            Add(findings, "InvalidAttemptOwner",
                "Measurement must belong to exactly one generation or tool attempt.");
        if (measurement.GenerationAttemptId.HasValue &&
            !string.Equals(measurement.ProducerLane, "testmap", StringComparison.Ordinal))
            Add(findings, "LaneOwnerMismatch", "Generation-attempt measurements require producer lane 'testmap'.");
        if (measurement.ToolAttemptId.HasValue &&
            !string.Equals(measurement.ProducerLane, "agent-tool", StringComparison.Ordinal))
            Add(findings, "LaneOwnerMismatch", "Tool-attempt measurements require producer lane 'agent-tool'.");
        if (!string.Equals(
                measurement.PolicyVersion,
                AssertionLineagePolicy.CurrentPolicyVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                measurement.AssertionCatalogVersion,
                AssertionLineagePolicy.CurrentCatalogVersion,
                StringComparison.Ordinal) ||
            measurement.MaxDepth is < 1 or > AssertionLineagePolicy.MaximumSupportedDepth)
            Add(findings, "UnsupportedPolicy",
                "Measurement requires supported policy, catalog, and maximum-depth values.");
        if (measurement.Status == AssertionMeasurementStatus.NotMeasured)
            Add(findings, "HistoricalStatusPersisted", "NotMeasured is derived and must not be persisted.");
        if (measurement.Status == AssertionMeasurementStatus.NotApplicable &&
            measurement.TestSummaries.Count > 0)
            Add(findings, "NotApplicableHasEligibleTests",
                "NotApplicable measurements cannot contain generated-test summaries.");
        if (measurement.Status is AssertionMeasurementStatus.Unavailable or AssertionMeasurementStatus.Partial)
            ValidateReason(measurement.FailureCode, measurement.FailureReason, "measurement", findings);

        if (measurement.Status is AssertionMeasurementStatus.Unavailable or
            AssertionMeasurementStatus.NotApplicable or
            AssertionMeasurementStatus.NotMeasured)
        {
            if (MeasurementCounts(measurement).Any(x => x.HasValue))
                Add(findings, "UnavailableCountsObserved",
                    $"{measurement.Status} measurement counts must be null.");
        }
        else
        {
            ValidateNonNegative(CategoryCounts(measurement), "measurement", findings);
            if (!Reconciles(
                    measurement.RecognizedAssertionCount,
                    measurement.TracedAssertionCount,
                    measurement.TrivialAssertionCount,
                    measurement.UnresolvedAssertionCount))
                Add(findings, "AttemptCountMismatch", "Attempt assertion category counts do not reconcile.");
        }

        foreach (var summary in measurement.TestSummaries)
        {
            if (measurement.Id > 0 &&
                summary.AssertionLineageMeasurementId != measurement.Id)
                Add(findings, "OrphanGeneratedTestSummary",
                    $"Generated-test summary {summary.Id} does not reference measurement {measurement.Id}.");
            ValidateSummary(summary, findings);
            if (summary.Observations.Any(x =>
                    x.DepthReached > measurement.MaxDepth ||
                    x.Steps.Any(step => step.Depth > measurement.MaxDepth)))
                Add(findings, "DepthPolicyMismatch",
                    $"Generated-test summary {summary.Id} exceeds the measurement maximum depth.");
        }

        if (measurement.Status is AssertionMeasurementStatus.Complete or AssertionMeasurementStatus.Partial)
        {
            var available = measurement.TestSummaries
                .Where(x => x.Status != GeneratedTestAssertionStatus.Unavailable)
                .ToList();
            if (measurement.EligibleTestCount != measurement.TestSummaries.Count ||
                measurement.AnalyzedTestCount != available.Count ||
                measurement.UnavailableTestCount !=
                measurement.TestSummaries.Count - available.Count ||
                measurement.NoRecognizedAssertionTestCount !=
                available.Count(x => x.Status == GeneratedTestAssertionStatus.NoRecognizedAssertions) ||
                measurement.RecognizedAssertionCount != Sum(available, x => x.RecognizedAssertionCount) ||
                measurement.UnrecognizedAssertionCount != Sum(available, x => x.UnrecognizedAssertionCount) ||
                measurement.TracedAssertionCount != Sum(available, x => x.TracedAssertionCount) ||
                measurement.TrivialAssertionCount != Sum(available, x => x.TrivialAssertionCount) ||
                measurement.UnresolvedAssertionCount != Sum(available, x => x.UnresolvedAssertionCount))
                Add(findings, "AttemptChildMismatch",
                    "Attempt aggregates do not equal their generated-test summaries.");
        }
    }

    private static void ValidateSummary(
        GeneratedTestAssertionSummary summary,
        ICollection<AssertionLineageAuditFinding> findings)
    {
        if (summary.GeneratedTestExecutionId.HasValue && summary.ToolAttemptGeneratedTestId.HasValue)
            Add(findings, "InvalidGeneratedTestOwner",
                "Generated-test summary cannot belong to both lane-specific owners.");
        if (!summary.TestMemberId.HasValue && string.IsNullOrWhiteSpace(summary.FallbackIdentityHash))
            Add(findings, "MissingGeneratedTestIdentity",
                "Generated-test summary requires a member or fallback identity hash.");

        if (summary.Status == GeneratedTestAssertionStatus.Unavailable)
        {
            ValidateReason(
                StableReasonCodePrefix(summary.StatusReason),
                summary.StatusReason,
                "generated-test summary",
                findings);
            if (SummaryCounts(summary).Any(x => x.HasValue))
                Add(findings, "UnavailableSummaryCountsObserved",
                    "Unavailable generated-test category counts must be null.");
            if (summary.Observations.Count > 0)
                Add(findings, "UnavailableSummaryHasObservations",
                    "Unavailable generated-test summary cannot contain classified observations.");
            return;
        }

        ValidateNonNegative(SummaryCounts(summary), "generated-test summary", findings);
        if (!Reconciles(
                summary.RecognizedAssertionCount,
                summary.TracedAssertionCount,
                summary.TrivialAssertionCount,
                summary.UnresolvedAssertionCount))
            Add(findings, "GeneratedTestCountMismatch",
                "Generated-test assertion category counts do not reconcile.");
        if (summary.RecognizedAssertionCount != summary.Observations.Count)
            Add(findings, "ObservationCountMismatch",
                "Generated-test recognized count does not equal its observation count.");
        if (summary.Status == GeneratedTestAssertionStatus.NoRecognizedAssertions &&
            (summary.RecognizedAssertionCount != 0 || summary.Observations.Count != 0))
            Add(findings, "InvalidNoRecognizedSummary",
                "NoRecognizedAssertions requires zero recognized assertions and observations.");
        if (summary.Status == GeneratedTestAssertionStatus.Classified &&
            summary.RecognizedAssertionCount is not > 0)
            Add(findings, "EmptyClassifiedSummary",
                "Classified generated-test summary requires at least one observation.");

        var duplicateOrdinals = summary.Observations.GroupBy(x => x.Ordinal).Any(x => x.Count() > 1);
        var duplicateHashes = summary.Observations
            .Where(x => !string.IsNullOrWhiteSpace(x.ExpressionHash))
            .GroupBy(x => x.ExpressionHash, StringComparer.Ordinal)
            .Any(x => x.Count() > 1);
        if (duplicateOrdinals || duplicateHashes)
            Add(findings, "DuplicateAssertionIdentity",
                "Generated-test summary contains duplicate assertion identity.");

        foreach (var observation in summary.Observations)
        {
            if (summary.Id > 0 &&
                observation.GeneratedTestAssertionSummaryId != summary.Id)
                Add(findings, "OrphanAssertionObservation",
                    $"Assertion observation {observation.Id} does not reference summary {summary.Id}.");
            ValidateObservation(observation, findings);
        }
    }

    private static void ValidateObservation(
        AssertionObservation observation,
        ICollection<AssertionLineageAuditFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(observation.ExpressionHash) ||
            string.IsNullOrWhiteSpace(observation.ResolutionCode) ||
            string.IsNullOrWhiteSpace(observation.TraceSummary))
            Add(findings, "IncompleteObservation",
                $"Assertion ordinal {observation.Ordinal} lacks identity or trace evidence.");
        if (!IsStableReasonCode(observation.ResolutionCode))
            Add(findings, "UnknownResolutionCode",
                $"Assertion ordinal {observation.Ordinal} has an unsupported resolution code.");
        // Recognition kind describes how the assertion terminal was identified; the category
        // describes where the operands came from. The two are independent: operand lineage is
        // resolved semantically whether or not the terminal bound to a symbol, which is the
        // normal case when a test framework ships in a package the compilation lacks. A
        // syntactically recognized terminal therefore does not cap what its operands may be
        // classified as.
        if (observation.Steps.Count == 0)
            Add(findings, "MissingLineagePath",
                $"Assertion ordinal {observation.Ordinal} has no lineage steps.");

        foreach (var path in observation.Steps.GroupBy(x => new { x.InputIndex, x.PathIndex }))
        {
            var ordered = path.OrderBy(x => x.StepIndex).ToList();
            if (!ordered.Select(x => x.StepIndex).SequenceEqual(Enumerable.Range(0, ordered.Count)))
                Add(findings, "NonContiguousLineagePath",
                    $"Assertion ordinal {observation.Ordinal} has non-contiguous lineage steps.");
            var terminals = ordered.Where(x => x.Outcome != AssertionLineageStepOutcome.Continue).ToList();
            if (terminals.Count != 1 || ordered.Count == 0 || terminals[0] != ordered[^1])
                Add(findings, "InvalidLineageTerminal",
                    $"Assertion ordinal {observation.Ordinal} path must end in exactly one terminal.");
        }

        foreach (var step in observation.Steps)
        {
            if (observation.Id > 0 && step.AssertionObservationId != observation.Id)
                Add(findings, "OrphanLineageStep",
                    $"Lineage step {step.Id} does not reference observation {observation.Id}.");
            if (!IsStableReasonCode(step.ReasonCode))
                Add(findings, "UnknownStepReasonCode",
                    $"Lineage step {step.Id} has an unsupported reason code.");
        }

        var hasProductionTerminal = observation.Steps.Any(x =>
            x.StepKind == AssertionLineageStepKind.ProductionMember &&
            x.Outcome == AssertionLineageStepOutcome.Traced &&
            x.MemberId.HasValue);
        var hasTracedTerminal = observation.Steps.Any(x => x.Outcome == AssertionLineageStepOutcome.Traced);
        var hasUnresolvedTerminal =
            observation.Steps.Any(x => x.Outcome == AssertionLineageStepOutcome.Unresolved);
        var allTerminalsTrivial = observation.Steps
            .Where(x => x.Outcome != AssertionLineageStepOutcome.Continue)
            .All(x => x.Outcome == AssertionLineageStepOutcome.Trivial);

        if (observation.Category == AssertionLineageCategory.Traced && !hasProductionTerminal)
            Add(findings, "TracedWithoutProduction",
                $"Assertion ordinal {observation.Ordinal} has no resolved production terminal.");
        if (observation.Category == AssertionLineageCategory.Trivial &&
            (!allTerminalsTrivial || hasProductionTerminal || hasUnresolvedTerminal))
            Add(findings, "InvalidTrivialEvidence",
                $"Assertion ordinal {observation.Ordinal} has non-trivial terminal evidence.");
        if (observation.Category == AssertionLineageCategory.Unresolved &&
            (!hasUnresolvedTerminal || hasTracedTerminal))
            Add(findings, "InvalidUnresolvedEvidence",
                $"Assertion ordinal {observation.Ordinal} has inconsistent unresolved evidence.");
    }

    private static void ValidateReason(
        string? code,
        string? reason,
        string owner,
        ICollection<AssertionLineageAuditFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(code) || !IsStableReasonCode(code))
            Add(findings, "MissingStableReason", $"{owner} requires a stable reason code.");
        if (string.IsNullOrWhiteSpace(reason))
            Add(findings, "MissingReasonSummary", $"{owner} requires a reason summary.");
    }

    private static string? StableReasonCodePrefix(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return reason;

        var separator = reason.IndexOf(':', StringComparison.Ordinal);
        return separator < 0 ? reason : reason[..separator];
    }

    private static void ValidateNonNegative(
        IEnumerable<int?> counts,
        string owner,
        ICollection<AssertionLineageAuditFinding> findings)
    {
        if (counts.Any(x => !x.HasValue || x.Value < 0))
            Add(findings, "InvalidCounts", $"{owner} requires non-negative observed counts.");
    }

    private static bool Reconciles(int? recognized, int? traced, int? trivial, int? unresolved) =>
        recognized.HasValue &&
        traced.HasValue &&
        trivial.HasValue &&
        unresolved.HasValue &&
        recognized.Value == traced.Value + trivial.Value + unresolved.Value;

    private static IEnumerable<int?> CategoryCounts(AssertionLineageMeasurement measurement)
    {
        yield return measurement.RecognizedAssertionCount;
        yield return measurement.UnrecognizedAssertionCount;
        yield return measurement.TracedAssertionCount;
        yield return measurement.TrivialAssertionCount;
        yield return measurement.UnresolvedAssertionCount;
    }

    private static IEnumerable<int?> MeasurementCounts(AssertionLineageMeasurement measurement)
    {
        yield return measurement.EligibleTestCount;
        yield return measurement.AnalyzedTestCount;
        yield return measurement.UnavailableTestCount;
        yield return measurement.NoRecognizedAssertionTestCount;
        foreach (var count in CategoryCounts(measurement))
            yield return count;
    }

    private static IEnumerable<int?> SummaryCounts(GeneratedTestAssertionSummary summary)
    {
        yield return summary.RecognizedAssertionCount;
        yield return summary.UnrecognizedAssertionCount;
        yield return summary.TracedAssertionCount;
        yield return summary.TrivialAssertionCount;
        yield return summary.UnresolvedAssertionCount;
    }

    private static int Sum(
        IEnumerable<GeneratedTestAssertionSummary> summaries,
        Func<GeneratedTestAssertionSummary, int?> selector) =>
        summaries.Sum(x => selector(x) ?? 0);

    private static void Add(
        ICollection<AssertionLineageAuditFinding> findings,
        string code,
        string message) =>
        findings.Add(new AssertionLineageAuditFinding(code, message));
}
