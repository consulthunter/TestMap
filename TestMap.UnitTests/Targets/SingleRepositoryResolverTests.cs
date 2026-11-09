using TestMap.Models.Targets;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.UnitTests.Targets;

public sealed class SingleRepositoryResolverTests
{
    [Fact]
    public async Task ResolveAsync_StablePass_UsesNamedBranchAndExactCommit()
    {
        var client = ScriptedClient.Stable(TargetTestData.Commit);
        var result = await Resolver(client).ResolveAsync(Request());
        Assert.Equal(SingleRepositoryResolutionStatus.Resolved, result.Status);
        Assert.Equal("main", result.DefaultBranch);
        Assert.Equal(TargetTestData.Commit, result.ResolvedCommit);
        Assert.Equal(1, result.ResolutionPasses);
        Assert.Equal(["repo", "branch:main", "commit:" + TargetTestData.Commit, "repo"], client.Calls);
    }

    [Fact]
    public async Task ResolveAsync_FirstPassInconsistent_RetriesWholePass()
    {
        var client = new ScriptedClient(
            new Queue<RepositoryResolutionMetadata>(
                [new("owner/repository", "main"), new("owner/repository", "trunk"),
                 new("owner/repository", "trunk"), new("owner/repository", "trunk")]),
            new Queue<RepositoryResolutionBranch>(
                [new("main", TargetTestData.Commit), new("trunk", TargetTestData.OtherCommit)]),
            new Queue<RepositoryResolutionCommit>(
                [new(TargetTestData.Commit), new(TargetTestData.OtherCommit)]));
        var result = await Resolver(client).ResolveAsync(Request());
        Assert.Equal(SingleRepositoryResolutionStatus.Resolved, result.Status);
        Assert.Equal(2, result.ResolutionPasses);
        Assert.Equal("trunk", result.DefaultBranch);
        Assert.Equal(TargetTestData.OtherCommit, result.ResolvedCommit);
    }

    [Fact]
    public async Task ResolveAsync_BothPassesInconsistent_FailsWithoutMixedFields()
    {
        var client = new ScriptedClient(
            new Queue<RepositoryResolutionMetadata>(
                [new("owner/repository", "main"), new("owner/repository", "trunk"),
                 new("owner/repository", "trunk"), new("owner/repository", "next")]),
            new Queue<RepositoryResolutionBranch>(
                [new("main", TargetTestData.Commit), new("trunk", TargetTestData.OtherCommit)]),
            new Queue<RepositoryResolutionCommit>(
                [new(TargetTestData.Commit), new(TargetTestData.OtherCommit)]));
        var result = await Resolver(client).ResolveAsync(Request());
        Assert.Equal(SingleRepositoryResolutionStatus.InconsistentResolution, result.Status);
        Assert.Equal(2, result.ResolutionPasses);
        Assert.Null(result.ResolvedCommit);
        Assert.Null(result.DefaultBranch);
    }

    [Fact]
    public async Task ResolveAsync_RateLimit_DoesNotRetry()
    {
        var client = new ThrowingClient(new RepositoryResolutionProviderException(
            RepositoryResolutionFailureKind.RateLimited, "rate limited", DateTimeOffset.UtcNow.AddMinutes(1)));
        var result = await Resolver(client).ResolveAsync(Request());
        Assert.Equal(SingleRepositoryResolutionStatus.RateLimited, result.Status);
        Assert.Equal(1, client.Calls);
        Assert.NotNull(result.RetryAfterUtc);
    }

    [Fact]
    public async Task ResolveAsync_RepositoryIdentityMismatch_StopsBeforeBranchLookup()
    {
        var client = new ScriptedClient(
            new Queue<RepositoryResolutionMetadata>([new("other/repository", "main")]),
            new Queue<RepositoryResolutionBranch>(),
            new Queue<RepositoryResolutionCommit>());

        var result = await Resolver(client).ResolveAsync(Request());

        Assert.Equal(SingleRepositoryResolutionStatus.RepositoryIdentityMismatch, result.Status);
        Assert.Equal(["repo"], client.Calls);
    }

    [Theory]
    [InlineData("different", TargetTestData.Commit, TargetTestData.Commit, SingleRepositoryResolutionStatus.DefaultBranchUnavailable)]
    [InlineData("main", "0000000000000000000000000000000000000000", TargetTestData.Commit, SingleRepositoryResolutionStatus.CommitUnavailable)]
    [InlineData("main", TargetTestData.Commit, TargetTestData.OtherCommit, SingleRepositoryResolutionStatus.CommitMismatch)]
    public async Task ResolveAsync_InvalidBranchOrCommit_IsCategorized(
        string returnedBranch,
        string branchCommit,
        string commitObject,
        SingleRepositoryResolutionStatus expected)
    {
        var client = new ScriptedClient(
            new Queue<RepositoryResolutionMetadata>([new("owner/repository", "main")]),
            new Queue<RepositoryResolutionBranch>([new(returnedBranch, branchCommit)]),
            new Queue<RepositoryResolutionCommit>([new(commitObject)]));

        var result = await Resolver(client).ResolveAsync(Request());

        Assert.Equal(expected, result.Status);
        Assert.Null(result.ResolvedCommit);
    }

    [Fact]
    public async Task ResolveAsync_ProviderMessage_IsRedactedAndBounded()
    {
        var secret = "github_pat_ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var client = new ThrowingClient(new RepositoryResolutionProviderException(
            RepositoryResolutionFailureKind.ServiceUnavailable,
            "Authorization: Bearer " + secret + "\n" + new string('x', 500)));

        var result = await Resolver(client).ResolveAsync(Request());

        Assert.Equal(SingleRepositoryResolutionStatus.ServiceUnavailable, result.Status);
        Assert.DoesNotContain(secret, result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', result.Summary);
        Assert.True(result.Summary.Length <= 300);
    }

    [Theory]
    [InlineData(RepositoryResolutionFailureKind.RepositoryUnavailable, SingleRepositoryResolutionStatus.RepositoryUnavailable)]
    [InlineData(RepositoryResolutionFailureKind.AuthenticationFailed, SingleRepositoryResolutionStatus.AuthenticationFailed)]
    [InlineData(RepositoryResolutionFailureKind.AuthorizationFailed, SingleRepositoryResolutionStatus.AuthorizationFailed)]
    [InlineData(RepositoryResolutionFailureKind.EmptyRepository, SingleRepositoryResolutionStatus.EmptyRepository)]
    [InlineData(RepositoryResolutionFailureKind.DefaultBranchUnavailable, SingleRepositoryResolutionStatus.DefaultBranchUnavailable)]
    [InlineData(RepositoryResolutionFailureKind.CommitUnavailable, SingleRepositoryResolutionStatus.CommitUnavailable)]
    [InlineData(RepositoryResolutionFailureKind.ServiceUnavailable, SingleRepositoryResolutionStatus.ServiceUnavailable)]
    [InlineData(RepositoryResolutionFailureKind.Failed, SingleRepositoryResolutionStatus.ResolutionFailed)]
    public async Task ResolveAsync_ProviderFailure_UsesStableStatusVocabulary(
        RepositoryResolutionFailureKind failure,
        SingleRepositoryResolutionStatus expected)
    {
        var result = await Resolver(new ThrowingClient(
            new RepositoryResolutionProviderException(failure, "provider failure")))
            .ResolveAsync(Request());

        Assert.Equal(expected, result.Status);
        Assert.NotNull(result.ReasonKind);
        Assert.Null(result.ResolvedCommit);
    }

    private static SingleRepositoryResolver Resolver(IRepositoryResolutionClient client)
    {
        var fingerprint = new TargetFingerprintService();
        return new SingleRepositoryResolver(
            client, new TargetIdentityService(), new SingleRepositoryResolutionSanitizer(),
            new SingleRepositoryResolutionValidator(fingerprint));
    }

    private static SingleRepositoryRequest Request()
    {
        var fingerprint = new TargetFingerprintService();
        return new SingleRepositoryUrlParser(new TargetIdentityService(), fingerprint)
            .Parse("https://github.com/owner/repository", RepositoryAuthenticationMode.Anonymous);
    }

    private sealed class ScriptedClient(
        Queue<RepositoryResolutionMetadata> metadata,
        Queue<RepositoryResolutionBranch> branches,
        Queue<RepositoryResolutionCommit> commits) : IRepositoryResolutionClient
    {
        public List<string> Calls { get; } = [];
        public static ScriptedClient Stable(string commit) => new(
            new Queue<RepositoryResolutionMetadata>([new("owner/repository", "main"), new("owner/repository", "main")]),
            new Queue<RepositoryResolutionBranch>([new("main", commit)]),
            new Queue<RepositoryResolutionCommit>([new(commit)]));
        public Task<RepositoryResolutionMetadata> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default)
        { Calls.Add("repo"); return Task.FromResult(metadata.Dequeue()); }
        public Task<RepositoryResolutionBranch> GetBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default)
        { Calls.Add("branch:" + branch); return Task.FromResult(branches.Dequeue()); }
        public Task<RepositoryResolutionCommit> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default)
        { Calls.Add("commit:" + commit); return Task.FromResult(commits.Dequeue()); }
    }

    private sealed class ThrowingClient(Exception exception) : IRepositoryResolutionClient
    {
        public int Calls { get; private set; }
        public Task<RepositoryResolutionMetadata> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromException<RepositoryResolutionMetadata>(exception); }
        public Task<RepositoryResolutionBranch> GetBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RepositoryResolutionCommit> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
