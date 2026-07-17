using TestMap.Models.Targets;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.EndToEndTests;

public sealed class SingleRepositoryTargetEndToEndTests
{
    [Theory]
    [InlineData(RepositoryResolutionFailureKind.RepositoryUnavailable, SingleRepositoryResolutionStatus.RepositoryUnavailable)]
    [InlineData(RepositoryResolutionFailureKind.AuthenticationFailed, SingleRepositoryResolutionStatus.AuthenticationFailed)]
    [InlineData(RepositoryResolutionFailureKind.AuthorizationFailed, SingleRepositoryResolutionStatus.AuthorizationFailed)]
    [InlineData(RepositoryResolutionFailureKind.RateLimited, SingleRepositoryResolutionStatus.RateLimited)]
    [InlineData(RepositoryResolutionFailureKind.EmptyRepository, SingleRepositoryResolutionStatus.EmptyRepository)]
    [InlineData(RepositoryResolutionFailureKind.DefaultBranchUnavailable, SingleRepositoryResolutionStatus.DefaultBranchUnavailable)]
    [InlineData(RepositoryResolutionFailureKind.CommitUnavailable, SingleRepositoryResolutionStatus.CommitUnavailable)]
    [InlineData(RepositoryResolutionFailureKind.ServiceUnavailable, SingleRepositoryResolutionStatus.ServiceUnavailable)]
    [InlineData(RepositoryResolutionFailureKind.Failed, SingleRepositoryResolutionStatus.ResolutionFailed)]
    public async Task ProviderFailure_RetainsSanitizedRecordAndPreservesCompletedManifest(
        RepositoryResolutionFailureKind failureKind,
        SingleRepositoryResolutionStatus expectedStatus)
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-url-failure-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "target.yaml");
        await File.WriteAllTextAsync(manifestPath, "previous-complete-manifest\n");
        try
        {
            var client = new FailingClient(failureKind);
            var exception = await Assert.ThrowsAsync<SingleRepositoryTargetCreationException>(() =>
                TestMap.Program.CreateSingleRepositoryTargetAsync(
                    "https://github.com/owner/repository", manifestPath,
                    resolutionClient: client));

            Assert.Equal(expectedStatus, exception.Resolution.Status);
            Assert.Equal(1, client.Calls);
            Assert.True(File.Exists(exception.ResolutionPath));
            Assert.Equal("previous-complete-manifest\n", await File.ReadAllTextAsync(manifestPath));
            var record = await File.ReadAllTextAsync(exception.ResolutionPath);
            Assert.DoesNotContain("SYNTHETIC_NOT_A_SECRET", record, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorization: Bearer", record, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task MovingDefaultBranch_CreatesIndependentImmutableBundles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-url-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var client = new MovingClient { Commit = CommitA };
            var first = await TestMap.Program.CreateSingleRepositoryTargetAsync(
                "https://github.com/owner/repository", Path.Combine(directory, "first.yaml"),
                resolutionClient: client);
            var firstManifestBytes = await File.ReadAllBytesAsync(first.ManifestPath);
            var firstResolutionBytes = await File.ReadAllBytesAsync(first.ResolutionPath);

            client.Commit = CommitB;
            var second = await TestMap.Program.CreateSingleRepositoryTargetAsync(
                "https://github.com/owner/repository", Path.Combine(directory, "second.yaml"),
                resolutionClient: client);

            Assert.Equal(CommitA, first.Manifest.Targets[0].Commit);
            Assert.Equal(CommitB, second.Manifest.Targets[0].Commit);
            Assert.Equal(firstManifestBytes, await File.ReadAllBytesAsync(first.ManifestPath));
            Assert.Equal(firstResolutionBytes, await File.ReadAllBytesAsync(first.ResolutionPath));
            Assert.NotEqual(first.ResolutionPath, second.ResolutionPath);
            var fingerprint = new TargetFingerprintService();
            Assert.Equal(first.Manifest.Resolution!.Sha256, fingerprint.ComputeSha256(firstResolutionBytes));
            Assert.Equal(second.Manifest.Resolution!.Sha256, await fingerprint.ComputeFileSha256Async(second.ResolutionPath));
        }
        finally { Directory.Delete(directory, true); }
    }

    private const string CommitA = "0123456789abcdef0123456789abcdef01234567";
    private const string CommitB = "89abcdef0123456789abcdef0123456789abcdef";

    private sealed class MovingClient : IRepositoryResolutionClient
    {
        public required string Commit { get; set; }
        public Task<RepositoryResolutionMetadata> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryResolutionMetadata(owner + "/" + repository, "main"));
        public Task<RepositoryResolutionBranch> GetBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryResolutionBranch(branch, Commit));
        public Task<RepositoryResolutionCommit> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryResolutionCommit(commit));
    }

    private sealed class FailingClient(RepositoryResolutionFailureKind failureKind) : IRepositoryResolutionClient
    {
        public int Calls { get; private set; }
        public Task<RepositoryResolutionMetadata> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default)
        {
            Calls++;
            DateTimeOffset? retry = failureKind == RepositoryResolutionFailureKind.RateLimited
                ? DateTimeOffset.UtcNow.AddMinutes(1)
                : null;
            return Task.FromException<RepositoryResolutionMetadata>(
                new RepositoryResolutionProviderException(
                    failureKind,
                    "Authorization: Bearer ghp_SYNTHETIC_NOT_A_SECRET",
                    retry));
        }
        public Task<RepositoryResolutionBranch> GetBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<RepositoryResolutionCommit> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
