namespace TestMap.Models.Targets;

public static class SingleRepositoryResolutionPolicy
{
    public const string Provider = "github.com";
    public const string Name = "github-default-branch-head";
    public const string Version = "1";
    public const string Identifier = Name + "/v" + Version;
}

public enum RepositoryAuthenticationMode
{
    Anonymous,
    Authenticated
}

public enum SingleRepositoryResolutionStatus
{
    Resolved,
    RepositoryUnavailable,
    RepositoryIdentityMismatch,
    AuthenticationFailed,
    AuthorizationFailed,
    RateLimited,
    EmptyRepository,
    DefaultBranchUnavailable,
    CommitUnavailable,
    CommitMismatch,
    InconsistentResolution,
    ServiceUnavailable,
    ResolutionFailed
}

public sealed record SingleRepositoryRequest(
    string RequestedUrl,
    string RequestedUrlSha256,
    string Repository,
    string CanonicalUrl,
    string Provider,
    string PolicyName,
    string PolicyVersion,
    RepositoryAuthenticationMode AuthenticationMode);

public sealed record SingleRepositoryResolution(
    int ResolutionSchemaVersion,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string RequestedUrl,
    string RequestedUrlSha256,
    string Repository,
    string CanonicalUrl,
    string Provider,
    string PolicyName,
    string PolicyVersion,
    RepositoryAuthenticationMode AuthenticationMode,
    SingleRepositoryResolutionStatus Status,
    string? ResolvedRepository,
    string? DefaultBranch,
    string? ResolvedCommit,
    int ResolutionPasses,
    string? ReasonKind,
    DateTimeOffset? RetryAfterUtc,
    string Summary);

public sealed record SingleRepositoryTargetCreationResult(
    TargetManifest Manifest,
    string ManifestPath,
    SingleRepositoryResolution Resolution,
    string ResolutionPath);

public sealed class SingleRepositoryTargetCreationException(
    SingleRepositoryResolution resolution,
    string resolutionPath) : InvalidOperationException(
        $"Target resolution failed: {resolution.Status} ({resolution.ReasonKind}).")
{
    public SingleRepositoryResolution Resolution { get; } = resolution;
    public string ResolutionPath { get; } = resolutionPath;
}
