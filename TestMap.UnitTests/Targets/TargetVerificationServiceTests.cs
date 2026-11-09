using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class TargetVerificationServiceTests
{
    [Theory]
    [InlineData(RemoteProbeStatus.Available, TargetVerificationStatus.Available)]
    [InlineData(RemoteProbeStatus.RepositoryUnavailable, TargetVerificationStatus.RepositoryUnavailable)]
    [InlineData(RemoteProbeStatus.AuthenticationFailed, TargetVerificationStatus.AuthenticationFailed)]
    [InlineData(RemoteProbeStatus.RateLimited, TargetVerificationStatus.RateLimited)]
    [InlineData(RemoteProbeStatus.CommitUnavailable, TargetVerificationStatus.CommitUnavailable)]
    public async Task VerifyAsync_MapsProbeStatus(RemoteProbeStatus probeStatus, TargetVerificationStatus expected)
    {
        var target = TargetTestData.Target();
        var resolved = probeStatus == RemoteProbeStatus.Available ? target.Commit : null;
        var service = new TargetVerificationService(new StubProbe(new RemoteProbeResult(probeStatus, resolved, "summary")));
        var result = await service.VerifyAsync(target, TargetTestData.Hash);
        Assert.Equal(expected, result.Status);
        Assert.Equal(resolved, result.ResolvedCommit);
        if (expected == TargetVerificationStatus.Available)
            Assert.Equal(result.RequestedCommit, result.ResolvedCommit);
    }

    [Fact]
    public async Task Coordinator_BoundedConcurrency_PreservesManifestOrder()
    {
        var targets = new[]
        {
            TargetTestData.Target(),
            TargetTestData.Target(TargetTestData.OtherCommit)
        };
        var coordinator = new TargetVerificationCoordinator(new DelayedVerifier());
        var records = await coordinator.VerifyAsync(TargetTestData.Manifest(targets), TargetTestData.Hash, 2);
        Assert.Equal(targets.Select(target => target.TargetId), records.Select(record => record.TargetId));
    }

    [Fact]
    public async Task VerifyAsync_WrongResolvedCommit_IsVerificationFailure()
    {
        var service = new TargetVerificationService(new StubProbe(
            new RemoteProbeResult(RemoteProbeStatus.Available, TargetTestData.OtherCommit, "wrong")));
        var result = await service.VerifyAsync(TargetTestData.Target(), TargetTestData.Hash);
        Assert.Equal(TargetVerificationStatus.VerificationFailed, result.Status);
        Assert.Null(result.ResolvedCommit);
    }

    private sealed class StubProbe(RemoteProbeResult result) : IRemoteRepositoryProbe
    {
        public Task<RemoteProbeResult> ProbeAsync(RepositoryTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class DelayedVerifier : TestMap.Services.Targets.Contracts.ITargetVerificationService
    {
        public async Task<TargetVerificationRecord> VerifyAsync(RepositoryTarget target, string manifestSha256, CancellationToken cancellationToken = default)
        {
            await Task.Delay(target.Commit == TargetTestData.Commit ? 30 : 1, cancellationToken);
            return new TargetVerificationRecord("1.0", manifestSha256, target.TargetId, target.Repository,
                target.Commit, target.Commit, TargetVerificationStatus.Available, DateTimeOffset.UtcNow, "available");
        }
    }
}
