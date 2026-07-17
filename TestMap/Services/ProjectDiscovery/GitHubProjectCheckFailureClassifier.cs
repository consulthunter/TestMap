using System.Net;
using Octokit;
using TestMap.Services.ProjectDiscovery.Contracts;

namespace TestMap.Services.ProjectDiscovery;

public sealed class GitHubProjectCheckFailureClassifier
{
    public ProjectTreeProviderException Classify(Exception exception, ProjectTreeFailureKind notFoundKind)
    {
        if (exception is ProjectTreeProviderException known) return known;
        var kind = exception switch
        {
            RateLimitExceededException => ProjectTreeFailureKind.RateLimited,
            AuthorizationException => ProjectTreeFailureKind.AuthenticationFailed,
            NotFoundException => notFoundKind,
            ApiException api when api.StatusCode == HttpStatusCode.Forbidden => ProjectTreeFailureKind.AuthorizationFailed,
            ApiException api when api.StatusCode == HttpStatusCode.Unauthorized => ProjectTreeFailureKind.AuthenticationFailed,
            ApiException api when api.StatusCode == HttpStatusCode.TooManyRequests => ProjectTreeFailureKind.RateLimited,
            ApiException api when (int)api.StatusCode >= 500 => ProjectTreeFailureKind.ServiceUnavailable,
            HttpRequestException => ProjectTreeFailureKind.ServiceUnavailable,
            _ => ProjectTreeFailureKind.Failed
        };
        return new ProjectTreeProviderException(kind, SafeMessage(kind), exception);
    }

    private static string SafeMessage(ProjectTreeFailureKind kind) => kind switch
    {
        ProjectTreeFailureKind.RepositoryUnavailable => "Repository is unavailable.",
        ProjectTreeFailureKind.AuthenticationFailed => "Repository authentication failed.",
        ProjectTreeFailureKind.AuthorizationFailed => "Repository access was not authorized.",
        ProjectTreeFailureKind.RateLimited => "Repository provider rate limit was exceeded.",
        ProjectTreeFailureKind.CommitUnavailable => "Requested commit is unavailable.",
        ProjectTreeFailureKind.TreeUnavailable => "Requested commit tree is unavailable.",
        ProjectTreeFailureKind.ServiceUnavailable => "Repository provider is temporarily unavailable.",
        _ => "Repository project check failed."
    };
}
