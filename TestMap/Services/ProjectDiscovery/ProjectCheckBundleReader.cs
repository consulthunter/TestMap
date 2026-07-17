using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery.Contracts;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.ProjectDiscovery;

public sealed record LoadedProjectCheckBundle(
    ProjectCheckBundle Bundle,
    ProjectCheckReport Report,
    TargetManifest TestsDetected,
    TargetManifest NoTestsDetected);

public sealed class ProjectCheckBundleReader(
    IProjectCheckSerializer checkSerializer,
    ITargetManifestSerializer targetSerializer,
    ProjectCheckContractValidator validator,
    TargetFingerprintService fingerprint)
{
    public async Task<LoadedProjectCheckBundle> ReadAsync(
        string bundlePath,
        CancellationToken cancellationToken = default)
    {
        var bundleBytes = await File.ReadAllBytesAsync(bundlePath, cancellationToken);
        var bundle = checkSerializer.DeserializeBundle(bundleBytes);
        var directory = Path.GetDirectoryName(Path.GetFullPath(bundlePath))!;
        var reportBytes = await ReadVerifiedAsync(directory, bundle.Artifacts.Report, cancellationToken);
        var positiveBytes = await ReadVerifiedAsync(directory, bundle.Artifacts.TestsDetected, cancellationToken);
        var negativeBytes = await ReadVerifiedAsync(directory, bundle.Artifacts.NoTestsDetected, cancellationToken);
        var report = checkSerializer.DeserializeReport(reportBytes);
        var positive = targetSerializer.Deserialize(positiveBytes);
        var negative = targetSerializer.Deserialize(negativeBytes);

        validator.ValidateReport(report);
        if (report.Input != bundle.Input || report.Policy != bundle.Policy ||
            report.Observations.Count != bundle.Artifacts.Report.Records ||
            positive.Targets.Count != bundle.Artifacts.TestsDetected.Records ||
            negative.Targets.Count != bundle.Artifacts.NoTestsDetected.Records ||
            positive.Derivation?.Category != "tests_detected" ||
            negative.Derivation?.Category != "no_tests_detected" ||
            positive.Source.Sha256 != bundle.Input.Sha256 || negative.Source.Sha256 != bundle.Input.Sha256 ||
            positive.Derivation.Report.Sha256 != bundle.Artifacts.Report.Sha256 ||
            negative.Derivation.Report.Sha256 != bundle.Artifacts.Report.Sha256)
            throw new InvalidDataException("Project-check bundle member provenance or counts do not agree.");

        var categorized = positive.Targets.Select(target => target.TargetId)
            .Concat(negative.Targets.Select(target => target.TargetId)).ToArray();
        if (categorized.Distinct(StringComparer.Ordinal).Count() != categorized.Length)
            throw new InvalidDataException("A target appears in multiple project-check categories.");
        var allowed = report.Observations
            .Where(row => row.Status is ProjectCheckStatus.TestsDetected or ProjectCheckStatus.NoTestsDetected)
            .Select(row => row.TargetId).ToHashSet(StringComparer.Ordinal);
        if (!categorized.ToHashSet(StringComparer.Ordinal).SetEquals(allowed))
            throw new InvalidDataException("Project-check categorized targets differ from determinate observations.");
        var observations = report.Observations.ToDictionary(row => row.TargetId, StringComparer.Ordinal);
        foreach (var target in positive.Targets.Concat(negative.Targets))
        {
            var observation = observations[target.TargetId];
            if (observation.Repository != target.Repository || observation.RequestedCommit != target.Commit)
                throw new InvalidDataException("Project-check categorized target provenance differs from its observation.");
        }

        return new LoadedProjectCheckBundle(bundle, report, positive, negative);
    }

    private async Task<byte[]> ReadVerifiedAsync(
        string directory,
        ProjectCheckArtifactReference artifact,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, artifact.File);
        if (!File.Exists(path)) throw new FileNotFoundException("Project-check bundle member is missing.", path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        if (fingerprint.ComputeSha256(bytes) != artifact.Sha256)
            throw new InvalidDataException("Project-check bundle member hash does not match.");
        return bytes;
    }
}
