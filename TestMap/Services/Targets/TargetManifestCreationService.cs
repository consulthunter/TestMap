using TestMap.Models.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.Targets;

public sealed class TargetManifestCreationService(
    ITargetImportService importer,
    ITargetManifestSerializer serializer,
    TargetRejectionReportWriter rejectionWriter,
    TargetFingerprintService fingerprint,
    IAtomicFilePublisher publisher)
{
    public async Task<TargetManifestCreationResult> CreateAsync(
        string inputPath,
        string outputPath,
        TargetDelimiter delimiter = TargetDelimiter.Auto,
        string? rejectionPath = null,
        CancellationToken cancellationToken = default)
    {
        var sourceBytes = await File.ReadAllBytesAsync(inputPath, cancellationToken);
        var sourceHash = fingerprint.ComputeSha256(sourceBytes);
        var imported = await importer.ImportAsync(inputPath, delimiter, cancellationToken);
        if (imported.Targets.Count == 0)
            throw new InvalidDataException("Target input did not contain any valid targets.");

        var rejectionBytes = rejectionWriter.Serialize(imported.Rejections);
        var rejectionHash = fingerprint.ComputeSha256(rejectionBytes);
        var outputFullPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(outputFullPath) ?? throw new InvalidDataException("Manifest output has no directory.");
        var rejectionBasePath = rejectionPath is null
            ? Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(outputFullPath)}-rejections.csv")
            : Path.GetFullPath(rejectionPath);
        var rejectionDirectory = Path.GetDirectoryName(rejectionBasePath)
            ?? throw new InvalidDataException("Rejection output has no directory.");
        var rejectionStem = Path.GetFileNameWithoutExtension(rejectionBasePath);
        var hashSuffix = rejectionHash[..12];
        var rejectionFullPath = Path.Combine(
            rejectionDirectory,
            rejectionStem.EndsWith(hashSuffix, StringComparison.OrdinalIgnoreCase)
                ? $"{rejectionStem}.csv"
                : $"{rejectionStem}-{hashSuffix}.csv");

        await publisher.PublishAsync(rejectionFullPath, rejectionBytes, cancellationToken);
        var publishedHash = await fingerprint.ComputeFileSha256Async(rejectionFullPath, cancellationToken);
        if (!string.Equals(rejectionHash, publishedHash, StringComparison.Ordinal))
            throw new IOException("Published rejection report hash does not match its content.");

        var manifest = new TargetManifest(
            1,
            DateTimeOffset.UtcNow,
            new TargetSourceProvenance(Path.GetFileName(inputPath), sourceHash),
            new TargetReportReference(Path.GetFileName(rejectionFullPath), rejectionHash),
            imported.Targets);
        var manifestBytes = serializer.Serialize(manifest);
        await publisher.PublishAsync(outputFullPath, manifestBytes, cancellationToken);

        return new TargetManifestCreationResult(
            manifest, outputFullPath, rejectionFullPath, imported.TotalRecords,
            imported.DeduplicatedRecords, imported.Rejections.Count);
    }
}
