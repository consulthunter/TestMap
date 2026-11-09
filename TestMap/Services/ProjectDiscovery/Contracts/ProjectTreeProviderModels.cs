namespace TestMap.Services.ProjectDiscovery.Contracts;

public sealed record ProjectRepositoryInfo(string FullName);

public sealed record ProjectCommitInfo(string Sha, string TreeSha);

public sealed record ProjectTreeEntry(string Path, string Type);

public sealed record ProjectTreeResult(IReadOnlyList<ProjectTreeEntry> Entries, bool Truncated);

public enum ProjectTreeFailureKind
{
    RepositoryUnavailable,
    AuthenticationFailed,
    AuthorizationFailed,
    RateLimited,
    CommitUnavailable,
    TreeUnavailable,
    ServiceUnavailable,
    Failed
}

public sealed class ProjectTreeProviderException(
    ProjectTreeFailureKind failureKind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public ProjectTreeFailureKind FailureKind { get; } = failureKind;
}
