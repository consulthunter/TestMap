namespace TestMap.Models.Targets;

public sealed record TargetManifest(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    TargetSourceProvenance Source,
    TargetReportReference? Rejections,
    IReadOnlyList<RepositoryTarget> Targets,
    TargetDerivationProvenance? Derivation = null,
    TargetReportReference? Resolution = null);

public sealed record TargetSourceProvenance(
    string? File,
    string Sha256,
    string? Kind = null,
    string? Url = null);

public sealed record TargetReportReference(string File, string Sha256);

public sealed record TargetDerivationProvenance(
    string Kind,
    string Category,
    string PolicyName,
    string PolicyVersion,
    TargetReportReference Report);

public sealed record RepositoryTarget(
    string TargetId,
    string Repository,
    string Url,
    string Commit,
    IReadOnlyList<int> SourceRows);

public sealed record TargetImportRecord(long SourceRow, string? Name, string? Commit);

public sealed record TargetImportResult(
    IReadOnlyList<RepositoryTarget> Targets,
    IReadOnlyList<TargetRejectionRecord> Rejections,
    int TotalRecords,
    int DeduplicatedRecords);

public sealed record TargetManifestCreationResult(
    TargetManifest Manifest,
    string ManifestPath,
    string RejectionReportPath,
    int TotalRecords,
    int DeduplicatedRecords,
    int RejectedRecords);
