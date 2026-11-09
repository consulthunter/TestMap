using System.Net;
using Octokit;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.UnitTests.Targets;

public sealed class GitHubRepositoryResolutionFailureClassifierTests
{
    [Fact]
    public void Classify_NotFound_UsesOperationSpecificKind()
    {
        var result = new GitHubRepositoryResolutionFailureClassifier().Classify(
            new NotFoundException("missing", HttpStatusCode.NotFound),
            RepositoryResolutionFailureKind.DefaultBranchUnavailable);

        Assert.Equal(RepositoryResolutionFailureKind.DefaultBranchUnavailable, result.FailureKind);
        Assert.DoesNotContain("missing", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_UnauthorizedAndTransportFailures_AreStable()
    {
        var classifier = new GitHubRepositoryResolutionFailureClassifier();

        Assert.Equal(
            RepositoryResolutionFailureKind.AuthenticationFailed,
            classifier.Classify(new AuthorizationException(), RepositoryResolutionFailureKind.RepositoryUnavailable).FailureKind);
        Assert.Equal(
            RepositoryResolutionFailureKind.ServiceUnavailable,
            classifier.Classify(new HttpRequestException("Authorization: Bearer secret"), RepositoryResolutionFailureKind.RepositoryUnavailable).FailureKind);
    }
}
