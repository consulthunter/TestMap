using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery;
using TestMap.Services.ProjectDiscovery.Contracts;
using TestMap.UnitTests.Targets;

namespace TestMap.UnitTests.ProjectDiscovery;

public sealed class ProjectCheckCoordinatorTests
{
    [Fact]
    public async Task CheckAsync_PreservesInputOrderAndRevisionIdentity()
    {
        var first = TargetTestData.Target();
        var second = TargetTestData.Target(TargetTestData.OtherCommit);
        var coordinator = Coordinator(new StubProbe(target => Observation(
            target,
            target == first ? ProjectCheckStatus.NoTestsDetected : ProjectCheckStatus.TestsDetected,
            target == first ? null : new ProjectCheckEvidence(ProjectCheckEvidenceCategory.TestProjectFile, "Tests/Tests.csproj"))));

        var observations = await coordinator.CheckAsync(TargetTestData.Manifest(first, second), 2);

        Assert.Equal([first.TargetId, second.TargetId], observations.Select(row => row.TargetId));
        Assert.Equal([first.Commit, second.Commit], observations.Select(row => row.ObservedCommit));
    }

    [Fact]
    public async Task CheckAsync_ThrownProbeStillProducesOneTerminalObservation()
    {
        var target = TargetTestData.Target();
        var coordinator = Coordinator(new StubProbe(_ => throw new InvalidOperationException("secret token")));

        var observation = Assert.Single(await coordinator.CheckAsync(TargetTestData.Manifest(target), 1));

        Assert.Equal(ProjectCheckStatus.CheckFailed, observation.Status);
        Assert.Equal("check_failed", observation.ReasonKind);
        Assert.DoesNotContain("secret", observation.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Probe_TruncatedTreeRequiresPositiveEvidenceForDeterminateResult()
    {
        var target = TargetTestData.Target();
        var noEvidence = new GitHubProjectTreeProbe(
            new StubClient(new ProjectTreeResult([new("src/Widget.cs", "blob")], true)),
            new ProjectTestPresencePolicy(), new ProjectCheckSanitizer());
        var withEvidence = new GitHubProjectTreeProbe(
            new StubClient(new ProjectTreeResult([new("tests/WidgetTests.cs", "blob")], true)),
            new ProjectTestPresencePolicy(), new ProjectCheckSanitizer());

        Assert.Equal(ProjectCheckStatus.TreeTruncated, (await noEvidence.CheckAsync(target)).Status);
        Assert.Equal(ProjectCheckStatus.TestsDetected, (await withEvidence.CheckAsync(target)).Status);
    }

    [Fact]
    public async Task CheckAsync_RespectsConcurrencyLimit()
    {
        var probe = new ConcurrentProbe();
        var targets = Enumerable.Range(1, 12)
            .Select(index => new TestMap.Services.Targets.TargetIdentityService().Create(
                $"owner/repository-{index}", index.ToString("x40"), [index]))
            .ToArray();

        await Coordinator(probe).CheckAsync(TargetTestData.Manifest(targets), 3);

        Assert.InRange(probe.MaximumConcurrency, 1, 3);
        Assert.Equal(12, probe.Calls);
    }

    private static ProjectCheckCoordinator Coordinator(IProjectCheckProbe probe) =>
        new(probe, new ProjectCheckContractValidator(), new ProjectCheckSanitizer());

    private static ProjectCheckObservation Observation(
        RepositoryTarget target, ProjectCheckStatus status, ProjectCheckEvidence? evidence) => new(
        target.TargetId, target.Repository, target.Commit, target.Commit, status,
        ProjectCheckPolicy.Name, ProjectCheckPolicy.Version, evidence?.Category, evidence?.Path, true,
        null, "complete", DateTimeOffset.UtcNow);

    private sealed class StubProbe(Func<RepositoryTarget, ProjectCheckObservation> factory) : IProjectCheckProbe
    {
        public Task<ProjectCheckObservation> CheckAsync(RepositoryTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult(factory(target));
    }

    private sealed class StubClient(ProjectTreeResult tree) : IProjectTreeClient
    {
        public Task<ProjectRepositoryInfo> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectRepositoryInfo(owner + "/" + repository));
        public Task<ProjectCommitInfo> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectCommitInfo(commit, "tree"));
        public Task<ProjectTreeResult> GetTreeAsync(string owner, string repository, string treeSha, CancellationToken cancellationToken = default) =>
            Task.FromResult(tree);
    }

    private sealed class ConcurrentProbe : IProjectCheckProbe
    {
        private int _active;
        public int Calls;
        public int MaximumConcurrency;

        public async Task<ProjectCheckObservation> CheckAsync(RepositoryTarget target, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            var active = Interlocked.Increment(ref _active);
            var current = MaximumConcurrency;
            while (active > current)
            {
                var observed = Interlocked.CompareExchange(ref MaximumConcurrency, active, current);
                if (observed == current) break;
                current = observed;
            }
            await Task.Delay(20, cancellationToken);
            Interlocked.Decrement(ref _active);
            return Observation(target, ProjectCheckStatus.NoTestsDetected, null);
        }
    }
}
