using System.Text.RegularExpressions;
using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.Services.ProjectDiscovery;

public sealed class ProjectCheckContractValidator
{
    public void ValidateObservation(ProjectCheckObservation observation)
    {
        RequireHash(observation.TargetId, "target_id");
        RequireCommit(observation.RequestedCommit, "requested_commit");
        if (observation.ObservedCommit is not null)
            RequireCommit(observation.ObservedCommit, "observed_commit");
        if (observation.CheckedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException("checked_at_utc must be UTC.");
        if (observation.PolicyName != ProjectCheckPolicy.Name || observation.PolicyVersion != ProjectCheckPolicy.Version)
            throw new InvalidDataException("Observation policy is unsupported.");
        if (string.IsNullOrWhiteSpace(observation.Repository) || string.IsNullOrWhiteSpace(observation.Summary))
            throw new InvalidDataException("Observation repository and summary are required.");
        var identity = new TargetIdentityService();
        if (!identity.TryNormalizeRepository(observation.Repository, out var repository) || repository != observation.Repository ||
            TargetIdentityService.CreateTargetId(repository, observation.RequestedCommit) != observation.TargetId)
            throw new InvalidDataException("Observation target identity does not match repository and requested commit.");

        var determinate = observation.Status is ProjectCheckStatus.TestsDetected or ProjectCheckStatus.NoTestsDetected;
        if (determinate && observation.ObservedCommit != observation.RequestedCommit)
            throw new InvalidDataException("Determinate observations require an exact observed commit.");
        if (observation.Status == ProjectCheckStatus.TestsDetected &&
            (observation.EvidenceCategory is null || string.IsNullOrWhiteSpace(observation.EvidencePath)))
            throw new InvalidDataException("TestsDetected requires evidence.");
        if (observation.Status != ProjectCheckStatus.TestsDetected &&
            (observation.EvidenceCategory is not null || observation.EvidencePath is not null))
            throw new InvalidDataException("Only TestsDetected may carry evidence.");
        if (observation.Status == ProjectCheckStatus.NoTestsDetected && observation.TreeComplete != true)
            throw new InvalidDataException("NoTestsDetected requires a complete tree.");
        if (observation.Status == ProjectCheckStatus.TreeTruncated && observation.TreeComplete != false)
            throw new InvalidDataException("TreeTruncated requires an incomplete tree.");
        if (determinate && observation.ReasonKind is not null)
            throw new InvalidDataException("Determinate observations must not carry a failure reason.");
        if (!determinate && string.IsNullOrWhiteSpace(observation.ReasonKind))
            throw new InvalidDataException("Indeterminate observations require a reason_kind.");
    }

    public void ValidateReport(ProjectCheckReport report, TargetManifest? input = null)
    {
        if (report.ReportSchemaVersion != 1 || report.GeneratedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Project-check report schema or timestamp is invalid.");
        ValidateInput(report.Input);
        ValidatePolicy(report.Policy);
        foreach (var observation in report.Observations) ValidateObservation(observation);
        if (report.Observations.Select(row => row.TargetId).Distinct(StringComparer.Ordinal).Count() != report.Observations.Count)
            throw new InvalidDataException("Project-check report contains duplicate target observations.");

        var byStatus = Enum.GetValues<ProjectCheckStatus>()
            .ToDictionary(status => status, status => report.Observations.Count(row => row.Status == status));
        if (report.Summary.InputTargets != report.Observations.Count ||
            report.Summary.TestsDetected != byStatus[ProjectCheckStatus.TestsDetected] ||
            report.Summary.NoTestsDetected != byStatus[ProjectCheckStatus.NoTestsDetected] ||
            report.Summary.Indeterminate != report.Observations.Count - report.Summary.TestsDetected - report.Summary.NoTestsDetected ||
            Enum.GetValues<ProjectCheckStatus>().Any(status => !report.Summary.ByStatus.TryGetValue(status, out var count) || count != byStatus[status]))
            throw new InvalidDataException("Project-check report summary does not match observations.");

        if (input is null) return;
        if (input.SchemaVersion != report.Input.SchemaVersion || input.Targets.Count != report.Observations.Count)
            throw new InvalidDataException("Project-check report does not account for the input manifest.");
        for (var index = 0; index < input.Targets.Count; index++)
        {
            var target = input.Targets[index];
            var observation = report.Observations[index];
            if (target.TargetId != observation.TargetId || target.Repository != observation.Repository || target.Commit != observation.RequestedCommit)
                throw new InvalidDataException("Project-check observation identity differs from the input target.");
        }
    }

    public void ValidateBundle(ProjectCheckBundle bundle)
    {
        if (bundle.BundleSchemaVersion != 1 || !bundle.Complete || bundle.GeneratedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Project-check bundle is not a completed schema-1 bundle.");
        ValidateInput(bundle.Input);
        ValidatePolicy(bundle.Policy);
        ValidateArtifact(bundle.Artifacts.Report, "report");
        ValidateArtifact(bundle.Artifacts.TestsDetected, "tests_detected");
        ValidateArtifact(bundle.Artifacts.NoTestsDetected, "no_tests_detected");
        var files = new[] { bundle.Artifacts.Report.File, bundle.Artifacts.TestsDetected.File, bundle.Artifacts.NoTestsDetected.File };
        if (files.Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length)
            throw new InvalidDataException("Project-check bundle member paths must be distinct.");
        var categorized = bundle.Artifacts.TestsDetected.Records + bundle.Artifacts.NoTestsDetected.Records;
        if (bundle.Artifacts.Report.Records < categorized ||
            (bundle.ClassificationComplete && bundle.Artifacts.Report.Records != categorized) ||
            (!bundle.ClassificationComplete && bundle.Artifacts.Report.Records == categorized))
            throw new InvalidDataException("Project-check bundle classification completeness disagrees with artifact counts.");
    }

    private static void ValidateInput(ProjectCheckInputReference input)
    {
        if (Path.GetFileName(input.File) != input.File || string.IsNullOrWhiteSpace(input.File))
            throw new InvalidDataException("Input file must be a basename.");
        RequireHash(input.Sha256, "input.sha256");
        if (input.SchemaVersion is not (1 or 2 or 3)) throw new InvalidDataException("Unsupported input target schema.");
    }

    private static void ValidatePolicy(ProjectCheckPolicyReference policy)
    {
        if (policy.Name != ProjectCheckPolicy.Name || policy.Version != ProjectCheckPolicy.Version)
            throw new InvalidDataException("Unsupported project-check policy.");
    }

    private static void ValidateArtifact(ProjectCheckArtifactReference artifact, string field)
    {
        if (Path.GetFileName(artifact.File) != artifact.File || string.IsNullOrWhiteSpace(artifact.File))
            throw new InvalidDataException(field + " file must be a basename.");
        RequireHash(artifact.Sha256, field + ".sha256");
        if (artifact.Records < 0) throw new InvalidDataException(field + " records cannot be negative.");
    }

    private static void RequireHash(string value, string field)
    {
        if (!Regex.IsMatch(value ?? string.Empty, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException(field + " must be a lower-case SHA-256 value.");
    }

    private static void RequireCommit(string value, string field)
    {
        if (!Regex.IsMatch(value ?? string.Empty, "^(?!0{40}$)[0-9a-f]{40}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException(field + " must be a full lower-case commit SHA.");
    }
}
