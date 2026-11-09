using TestMap.IntegrationTests.Fixtures;
using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.IntegrationTests.Targets;

public sealed class TargetVerificationIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task VerifyAsync_LocalBareRemote_DistinguishesAvailableAndMissingCommits()
    {
        using var fixture = GitRepositoryFixture.Create();
        var service = new TargetVerificationService(new LibGitRemoteRepositoryProbe());
        var availableTarget = Target(fixture.RemotePath, fixture.Commits[0]);
        var missingTarget = Target(fixture.RemotePath, "ffffffffffffffffffffffffffffffffffffffff");

        var available = await service.VerifyAsync(availableTarget, Hash);
        var missing = await service.VerifyAsync(missingTarget, Hash);

        Assert.Equal(TargetVerificationStatus.Available, available.Status);
        Assert.Equal(fixture.Commits[0], available.ResolvedCommit);
        Assert.Equal(TargetVerificationStatus.CommitUnavailable, missing.Status);
        Assert.Null(missing.ResolvedCommit);
    }

    private static RepositoryTarget Target(string url, string commit) => new(
        TargetIdentityService.CreateTargetId("local/repository", commit),
        "local/repository", url, commit, [1]);

    private const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
}
