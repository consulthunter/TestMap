using TestMap.Models.Targets;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;
using TestMap.Services.ProjectDiscovery;

namespace TestMap.Services.Configuration;

public sealed class TargetSourceReader(ITargetManifestSerializer serializer, TargetFingerprintService fingerprint) : ITargetSourceReader
{
    public async Task<TargetSource> ReadAsync(string path, TargetSourceMode mode, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Target source does not exist.", path);
        var extension = Path.GetExtension(path);
        var looksLikeManifest = extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
        if (!looksLikeManifest)
        {
            if (mode != TargetSourceMode.Discovery)
                throw new InvalidDataException("This command requires a pinned target YAML manifest; URL-only target lists are not accepted.");
            var lines = await File.ReadAllLinesAsync(path, cancellationToken);
            return new TargetSource(null, null, [], lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).ToArray(), true);
        }

        var (manifest, hash) = await serializer.ReadAsync(path, cancellationToken);
        var report = manifest.SchemaVersion switch
        {
            1 => manifest.Rejections ?? throw new InvalidDataException("Source target manifest has no rejection report."),
            2 => manifest.Derivation?.Report ?? throw new InvalidDataException("Derived target manifest has no project-check report."),
            3 => manifest.Resolution ?? throw new InvalidDataException("URL target manifest has no resolution report."),
            _ => throw new InvalidDataException("Unsupported target manifest schema.")
        };
        var reportPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, report.File);
        if (!File.Exists(reportPath))
            throw new InvalidDataException("Target manifest provenance report is missing or has changed.");
        var reportBytes = await File.ReadAllBytesAsync(reportPath, cancellationToken);
        if (!string.Equals(fingerprint.ComputeSha256(reportBytes), report.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException("Target manifest provenance report is missing or has changed.");
        if (manifest.SchemaVersion == 2)
            ValidateDerivedManifest(manifest, reportBytes);
        if (manifest.SchemaVersion == 3)
            ValidateUrlManifest(manifest, reportBytes);
        return new TargetSource(manifest, hash, manifest.Targets, [], false);
    }

    private static void ValidateUrlManifest(TargetManifest manifest, ReadOnlySpan<byte> reportBytes)
    {
        var fingerprint = new TargetFingerprintService();
        var resolutionValidator = new SingleRepositoryResolutionValidator(fingerprint);
        var resolution = new SingleRepositoryResolutionSerializer(resolutionValidator).Deserialize(reportBytes);
        new SingleRepositoryTargetContractValidator(resolutionValidator, fingerprint)
            .Validate(manifest, resolution);
    }

    private static void ValidateDerivedManifest(TargetManifest manifest, ReadOnlySpan<byte> reportBytes)
    {
        var derivation = manifest.Derivation!;
        var validator = new ProjectCheckContractValidator();
        var report = new ProjectCheckSerializer(validator).DeserializeReport(reportBytes);
        if (report.Input.Sha256 != manifest.Source.Sha256 ||
            report.Input.File != manifest.Source.File ||
            report.Policy.Name != derivation.PolicyName ||
            report.Policy.Version != derivation.PolicyVersion)
            throw new InvalidDataException("Derived target manifest provenance differs from its project-check report.");
        var expectedStatus = derivation.Category == "tests_detected"
            ? ProjectCheckStatus.TestsDetected
            : ProjectCheckStatus.NoTestsDetected;
        var observations = report.Observations.ToDictionary(row => row.TargetId, StringComparer.Ordinal);
        foreach (var target in manifest.Targets)
        {
            if (!observations.TryGetValue(target.TargetId, out var observation) ||
                observation.Status != expectedStatus ||
                observation.Repository != target.Repository ||
                observation.RequestedCommit != target.Commit)
                throw new InvalidDataException("Derived target is not supported by its declared project-check category.");
        }
    }
}
