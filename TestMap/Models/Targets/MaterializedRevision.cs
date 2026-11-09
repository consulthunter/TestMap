namespace TestMap.Models.Targets;

public enum MaterializationStatus
{
    Pending,
    Available,
    InvalidWorkspace,
    WrongOrigin,
    WorkspaceDirty,
    WorkspaceBusy,
    AuthenticationFailed,
    RepositoryUnavailable,
    CommitUnavailable,
    Failed
}

public sealed record TargetPaths(
    string WorkspacePath,
    string DatabasePath,
    string ArtifactPath,
    string LogPath);

public sealed record MaterializedRevision(
    string TargetId,
    string RepositoryIdentity,
    string RequestedCommit,
    string? ResolvedCommit,
    string? OriginUrl,
    TargetPaths Paths,
    string ManifestSha256,
    string SourceSha256,
    DateTimeOffset? MaterializedAtUtc,
    MaterializationStatus Status,
    string PolicyVersion = "pinned-target-v1");

public sealed class RepositoryMaterializationException : InvalidOperationException
{
    public RepositoryMaterializationException(MaterializationStatus status, string message, Exception? inner = null)
        : base(message, inner) => Status = status;

    public MaterializationStatus Status { get; }
}
