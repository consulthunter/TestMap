using System.Net;
using Octokit;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.Targets;

public sealed class GitHubRepositoryResolutionFailureClassifier
{
    public RepositoryResolutionProviderException Classify(
        Exception exception,
        RepositoryResolutionFailureKind notFoundKind)
    {
        if (exception is RepositoryResolutionProviderException known) return known;
        var kind = exception switch
        {
            RateLimitExceededException => RepositoryResolutionFailureKind.RateLimited,
            AuthorizationException => RepositoryResolutionFailureKind.AuthenticationFailed,
            NotFoundException => notFoundKind,
            ApiException api when api.StatusCode == HttpStatusCode.Unauthorized => RepositoryResolutionFailureKind.AuthenticationFailed,
            ApiException api when api.StatusCode == HttpStatusCode.Forbidden => RepositoryResolutionFailureKind.AuthorizationFailed,
            ApiException api when api.StatusCode == HttpStatusCode.TooManyRequests => RepositoryResolutionFailureKind.RateLimited,
            ApiException api when (int)api.StatusCode >= 500 => RepositoryResolutionFailureKind.ServiceUnavailable,
            HttpRequestException => RepositoryResolutionFailureKind.ServiceUnavailable,
            _ => RepositoryResolutionFailureKind.Failed
        };
        DateTimeOffset? retryAfter = exception is RateLimitExceededException rate ? rate.Reset : null;
        return new RepositoryResolutionProviderException(kind, SafeMessage(kind), retryAfter, exception);
    }

    private static string SafeMessage(RepositoryResolutionFailureKind kind) => kind switch
    {
        RepositoryResolutionFailureKind.RepositoryUnavailable => "Repository is unavailable.",
        RepositoryResolutionFailureKind.AuthenticationFailed => "Repository authentication failed.",
        RepositoryResolutionFailureKind.AuthorizationFailed => "Repository access was not authorized.",
        RepositoryResolutionFailureKind.RateLimited => "Repository provider rate limit was exceeded.",
        RepositoryResolutionFailureKind.EmptyRepository => "Repository has no resolvable commits.",
        RepositoryResolutionFailureKind.DefaultBranchUnavailable => "Repository default branch is unavailable.",
        RepositoryResolutionFailureKind.CommitUnavailable => "Repository commit is unavailable.",
        RepositoryResolutionFailureKind.ServiceUnavailable => "Repository provider is temporarily unavailable.",
        _ => "Repository resolution failed."
    };
}
