using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery.Contracts;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.ProjectDiscovery;

public sealed class ProjectCheckBundlePublisher(
    IProjectCheckSerializer checkSerializer,
    ITargetManifestSerializer targetSerializer,
    ProjectCheckPartitionService partitionService,
    ProjectCheckContractValidator validator,
    ProjectCheckOutputPathResolver pathResolver,
    TargetFingerprintService fingerprint,
    IAtomicFilePublisher publisher)
{
    public async Task<ProjectCheckPublicationResult> PublishAsync(
        string inputPath,
        string inputSha256,
        TargetManifest input,
        ProjectCheckReport report,
        string? bundlePath = null,
        CancellationToken cancellationToken = default)
    {
        validator.ValidateReport(report, input);
        if (!string.Equals(report.Input.Sha256, inputSha256, StringComparison.Ordinal) ||
            !string.Equals(report.Input.File, Path.GetFileName(inputPath), StringComparison.Ordinal))
            throw new InvalidDataException("Project-check report provenance differs from the input manifest.");

        var paths = pathResolver.Resolve(inputPath, bundlePath);
        var reportBytes = checkSerializer.SerializeReport(report);
        var reportHash = fingerprint.ComputeSha256(reportBytes);
        var reportPath = ProjectCheckOutputPathResolver.ContentAddressedPath(paths, "report", reportHash);
        var reportReference = new TargetReportReference(Path.GetFileName(reportPath), reportHash);
        var partition = partitionService.Partition(input, inputPath, inputSha256, report, reportReference, report.GeneratedAtUtc);

        var positiveBytes = targetSerializer.Serialize(partition.TestsDetected);
        var positiveHash = fingerprint.ComputeSha256(positiveBytes);
        var positivePath = ProjectCheckOutputPathResolver.ContentAddressedPath(paths, "tests-detected", positiveHash);
        var negativeBytes = targetSerializer.Serialize(partition.NoTestsDetected);
        var negativeHash = fingerprint.ComputeSha256(negativeBytes);
        var negativePath = ProjectCheckOutputPathResolver.ContentAddressedPath(paths, "no-tests-detected", negativeHash);

        EnsureDistinct(paths.BundlePath, inputPath, reportPath, positivePath, negativePath);
        Directory.CreateDirectory(paths.Directory);
        await PublishAndVerifyAsync(reportPath, reportBytes, reportHash, cancellationToken);
        await PublishAndVerifyAsync(positivePath, positiveBytes, positiveHash, cancellationToken);
        await PublishAndVerifyAsync(negativePath, negativeBytes, negativeHash, cancellationToken);

        var bundle = new ProjectCheckBundle(
            1,
            report.GeneratedAtUtc,
            report.Input,
            report.Policy,
            true,
            report.Summary.Indeterminate == 0,
            new ProjectCheckArtifacts(
                new ProjectCheckArtifactReference(Path.GetFileName(reportPath), reportHash, report.Observations.Count),
                new ProjectCheckArtifactReference(Path.GetFileName(positivePath), positiveHash, partition.TestsDetected.Targets.Count),
                new ProjectCheckArtifactReference(Path.GetFileName(negativePath), negativeHash, partition.NoTestsDetected.Targets.Count)));
        validator.ValidateBundle(bundle);
        var bundleBytes = checkSerializer.SerializeBundle(bundle);
        await publisher.PublishAsync(paths.BundlePath, bundleBytes, cancellationToken);

        return new ProjectCheckPublicationResult(
            bundle, paths.BundlePath, reportPath, positivePath, negativePath, report);
    }

    private async Task PublishAndVerifyAsync(
        string path, ReadOnlyMemory<byte> bytes, string expectedHash, CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(path, bytes, cancellationToken);
        var actualHash = await fingerprint.ComputeFileSha256Async(path, cancellationToken);
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
            throw new IOException("Published project-check artifact hash does not match its content.");
    }

    private static void EnsureDistinct(params string[] paths)
    {
        var full = paths.Select(Path.GetFullPath).ToArray();
        if (full.Distinct(StringComparer.OrdinalIgnoreCase).Count() != full.Length)
            throw new InvalidDataException("Project-check input and output paths must be distinct.");
    }
}
