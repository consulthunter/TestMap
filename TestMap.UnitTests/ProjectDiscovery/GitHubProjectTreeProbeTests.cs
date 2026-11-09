using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery;
using TestMap.Services.ProjectDiscovery.Contracts;
using TestMap.UnitTests.Targets;

namespace TestMap.UnitTests.ProjectDiscovery;

public sealed class GitHubProjectTreeProbeTests
{
    [Fact]
    public async Task CheckAsync_UsesRequestedCommitAndReturnedTreeSha()
    {
        var target = TargetTestData.Target();
        var client = new RecordingClient(target.Commit);
        var probe = new GitHubProjectTreeProbe(client, new ProjectTestPresencePolicy(), new ProjectCheckSanitizer());

        var result = await probe.CheckAsync(target);

        Assert.Equal(ProjectCheckStatus.NoTestsDetected, result.Status);
        Assert.Equal(target.Commit, client.RequestedCommit);
        Assert.Equal("tree-sha", client.RequestedTree);
        Assert.Equal(target.Commit, result.ObservedCommit);
    }

    [Theory]
    [InlineData(ProjectTreeFailureKind.RepositoryUnavailable, ProjectCheckStatus.RepositoryUnavailable)]
    [InlineData(ProjectTreeFailureKind.AuthenticationFailed, ProjectCheckStatus.AuthenticationFailed)]
    [InlineData(ProjectTreeFailureKind.AuthorizationFailed, ProjectCheckStatus.AuthorizationFailed)]
    [InlineData(ProjectTreeFailureKind.RateLimited, ProjectCheckStatus.RateLimited)]
    [InlineData(ProjectTreeFailureKind.CommitUnavailable, ProjectCheckStatus.CommitUnavailable)]
    [InlineData(ProjectTreeFailureKind.TreeUnavailable, ProjectCheckStatus.TreeUnavailable)]
    [InlineData(ProjectTreeFailureKind.ServiceUnavailable, ProjectCheckStatus.ServiceUnavailable)]
    [InlineData(ProjectTreeFailureKind.Failed, ProjectCheckStatus.CheckFailed)]
    public async Task CheckAsync_MapsProviderFailureWithoutLeakingRawMessage(
        ProjectTreeFailureKind failure,
        ProjectCheckStatus expected)
    {
        var probe = new GitHubProjectTreeProbe(
            new FailingClient(failure), new ProjectTestPresencePolicy(), new ProjectCheckSanitizer());
        var result = await probe.CheckAsync(TargetTestData.Target());
        Assert.Equal(expected, result.Status);
        Assert.Null(result.EvidencePath);
    }

    private sealed class RecordingClient(string commit) : IProjectTreeClient
    {
        public string? RequestedCommit { get; private set; }
        public string? RequestedTree { get; private set; }
        public Task<ProjectRepositoryInfo> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectRepositoryInfo(owner + "/" + repository));
        public Task<ProjectCommitInfo> GetCommitAsync(string owner, string repository, string requested, CancellationToken cancellationToken = default)
        {
            RequestedCommit = requested;
            return Task.FromResult(new ProjectCommitInfo(commit, "tree-sha"));
        }
        public Task<ProjectTreeResult> GetTreeAsync(string owner, string repository, string treeSha, CancellationToken cancellationToken = default)
        {
            RequestedTree = treeSha;
            return Task.FromResult(new ProjectTreeResult([new("src/Widget.cs", "blob")], false));
        }
    }

    private sealed class FailingClient(ProjectTreeFailureKind failure) : IProjectTreeClient
    {
        public Task<ProjectRepositoryInfo> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default) =>
            Task.FromException<ProjectRepositoryInfo>(new ProjectTreeProviderException(failure, "safe"));
        public Task<ProjectCommitInfo> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProjectTreeResult> GetTreeAsync(string owner, string repository, string treeSha, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
