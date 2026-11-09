using TestMap.Models.Targets;

namespace TestMap.Services.Targets.Contracts;

public interface ITargetManifestSerializer
{
    byte[] Serialize(TargetManifest manifest);
    TargetManifest Deserialize(ReadOnlySpan<byte> yaml);
    Task<(TargetManifest Manifest, string Sha256)> ReadAsync(string path, CancellationToken cancellationToken = default);
}

public interface ITargetImportService
{
    Task<TargetImportResult> ImportAsync(string path, TargetDelimiter delimiter = TargetDelimiter.Auto, CancellationToken cancellationToken = default);
}

public interface ITargetVerificationService
{
    Task<TargetVerificationRecord> VerifyAsync(RepositoryTarget target, string manifestSha256, CancellationToken cancellationToken = default);
}

public interface ITargetSourceReader
{
    Task<TargetSource> ReadAsync(string path, TargetSourceMode mode, CancellationToken cancellationToken = default);
}

public enum TargetDelimiter { Auto, Comma, Tab }
public enum TargetSourceMode { Discovery, PinnedManifest, MeasuredExperiment }

public sealed record TargetSource(
    TargetManifest? Manifest,
    string? ManifestSha256,
    IReadOnlyList<RepositoryTarget> Targets,
    IReadOnlyList<string> LegacyUrls,
    bool IsLegacy);
