namespace TestMap.Services.Targets.Contracts;

public sealed record RepositoryResolutionMetadata(string FullName, string? DefaultBranch);
public sealed record RepositoryResolutionBranch(string Name, string CommitSha);
public sealed record RepositoryResolutionCommit(string Sha);

public enum RepositoryResolutionFailureKind
{
    RepositoryUnavailable,
    AuthenticationFailed,
    AuthorizationFailed,
    RateLimited,
    EmptyRepository,
    DefaultBranchUnavailable,
    CommitUnavailable,
    ServiceUnavailable,
    Failed
}

public sealed class RepositoryResolutionProviderException(
    RepositoryResolutionFailureKind failureKind,
    string message,
    DateTimeOffset? retryAfterUtc = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public RepositoryResolutionFailureKind FailureKind { get; } = failureKind;
    public DateTimeOffset? RetryAfterUtc { get; } = retryAfterUtc;
}
