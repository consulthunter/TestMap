using TestMap.Models.Targets;
using TestMap.Services.Configuration;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.UnitTests.Targets;

public sealed class SingleRepositoryTargetCreationServiceTests
{
    [Fact]
    public async Task CreateAsync_Success_PublishesSchema3AndReloadsWithoutProvider()
    {
        var directory = CreateDirectory();
        try
        {
            var manifestPath = Path.Combine(directory, "target.yaml");
            var client = new StableClient(TargetTestData.Commit);
            var result = await TestMap.Program.CreateSingleRepositoryTargetAsync(
                "https://github.com/owner/repository", manifestPath, githubToken: null,
                resolutionClient: client);
            Assert.Equal(3, result.Manifest.SchemaVersion);
            Assert.Single(result.Manifest.Targets);
            Assert.Equal([1], result.Manifest.Targets[0].SourceRows);
            Assert.True(File.Exists(result.ResolutionPath));
            Assert.True(File.Exists(result.ManifestPath));

            var fingerprint = new TargetFingerprintService();
            var loaded = await new TargetSourceReader(
                    new TargetManifestSerializer(fingerprint), fingerprint)
                .ReadAsync(manifestPath, TargetSourceMode.PinnedManifest);
            Assert.Equal(TargetTestData.Commit, Assert.Single(loaded.Targets).Commit);
            Assert.Equal(4, client.Calls);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CreateAsync_Failure_PublishesRecordAndPreservesManifest()
    {
        var directory = CreateDirectory();
        try
        {
            var manifestPath = Path.Combine(directory, "target.yaml");
            await File.WriteAllTextAsync(manifestPath, "previous\n");
            var exception = await Assert.ThrowsAsync<SingleRepositoryTargetCreationException>(() =>
                TestMap.Program.CreateSingleRepositoryTargetAsync(
                    "https://github.com/owner/repository", manifestPath,
                    resolutionClient: new FailingClient()));
            Assert.Equal(SingleRepositoryResolutionStatus.RepositoryUnavailable, exception.Resolution.Status);
            Assert.True(File.Exists(exception.ResolutionPath));
            Assert.Equal("previous\n", await File.ReadAllTextAsync(manifestPath));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CreateAsync_InvalidUrl_PerformsNoProviderCallOrPublication()
    {
        var directory = CreateDirectory();
        try
        {
            var client = new CountingClient();
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                TestMap.Program.CreateSingleRepositoryTargetAsync(
                    "https://example.com/owner/repository", Path.Combine(directory, "target.yaml"),
                    resolutionClient: client));
            Assert.Equal(0, client.Calls);
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CreateAsync_CustomResolutionPath_RecordsAuthenticatedModeAndPublishesManifestLast()
    {
        var directory = CreateDirectory();
        try
        {
            var manifestPath = Path.Combine(directory, "target.yaml");
            var resolutionBasePath = Path.Combine(directory, "audit.yaml");
            var publisher = new RecordingPublisher();
            var result = await CreateService(new StableClient(TargetTestData.Commit), publisher)
                .CreateAsync(
                    "https://github.com/owner/repository",
                    manifestPath,
                    resolutionBasePath,
                    RepositoryAuthenticationMode.Authenticated);

            Assert.Equal(RepositoryAuthenticationMode.Authenticated, result.Resolution.AuthenticationMode);
            Assert.Matches("audit-[0-9a-f]{12}\\.yaml", Path.GetFileName(result.ResolutionPath));
            Assert.Equal([result.ResolutionPath, manifestPath], publisher.Paths);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CreateAsync_ManifestPublicationFailure_PreservesPreviousManifest()
    {
        var directory = CreateDirectory();
        try
        {
            var manifestPath = Path.Combine(directory, "target.yaml");
            await File.WriteAllTextAsync(manifestPath, "previous-complete-manifest\n");
            var publisher = new FailingManifestPublisher(manifestPath);

            await Assert.ThrowsAsync<IOException>(() =>
                CreateService(new StableClient(TargetTestData.Commit), publisher).CreateAsync(
                    "https://github.com/owner/repository",
                    manifestPath,
                    null,
                    RepositoryAuthenticationMode.Anonymous));

            Assert.Equal("previous-complete-manifest\n", await File.ReadAllTextAsync(manifestPath));
            Assert.Single(Directory.EnumerateFiles(directory, "*resolution-*.yaml"));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "testmap-single-target-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static SingleRepositoryTargetCreationService CreateService(
        IRepositoryResolutionClient client,
        IAtomicFilePublisher publisher)
    {
        var identity = new TargetIdentityService();
        var fingerprint = new TargetFingerprintService();
        var resolutionValidator = new SingleRepositoryResolutionValidator(fingerprint);
        return new SingleRepositoryTargetCreationService(
            new SingleRepositoryUrlParser(identity, fingerprint),
            new SingleRepositoryResolver(
                client, identity, new SingleRepositoryResolutionSanitizer(), resolutionValidator),
            new SingleRepositoryResolutionSerializer(resolutionValidator),
            new TargetManifestSerializer(fingerprint),
            identity,
            fingerprint,
            new SingleRepositoryTargetContractValidator(resolutionValidator, fingerprint),
            new SingleRepositoryTargetPathResolver(),
            publisher);
    }

    private sealed class StableClient(string commit) : IRepositoryResolutionClient
    {
        public int Calls { get; private set; }
        public Task<RepositoryResolutionMetadata> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new RepositoryResolutionMetadata(owner + "/" + repository, "main")); }
        public Task<RepositoryResolutionBranch> GetBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new RepositoryResolutionBranch(branch, commit)); }
        public Task<RepositoryResolutionCommit> GetCommitAsync(string owner, string repository, string requested, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new RepositoryResolutionCommit(commit)); }
    }

    private sealed class FailingClient : IRepositoryResolutionClient
    {
        public Task<RepositoryResolutionMetadata> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default) =>
            Task.FromException<RepositoryResolutionMetadata>(new RepositoryResolutionProviderException(
                RepositoryResolutionFailureKind.RepositoryUnavailable, "unavailable"));
        public Task<RepositoryResolutionBranch> GetBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RepositoryResolutionCommit> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CountingClient : IRepositoryResolutionClient
    {
        public int Calls { get; private set; }
        public Task<RepositoryResolutionMetadata> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default)
        { Calls++; throw new NotSupportedException(); }
        public Task<RepositoryResolutionBranch> GetBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RepositoryResolutionCommit> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingPublisher : IAtomicFilePublisher
    {
        private readonly AtomicFilePublisher _inner = new();
        public List<string> Paths { get; } = [];
        public Task PublishAsync(string destinationPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            Paths.Add(destinationPath);
            return _inner.PublishAsync(destinationPath, content, cancellationToken);
        }
    }

    private sealed class FailingManifestPublisher(string manifestPath) : IAtomicFilePublisher
    {
        private readonly AtomicFilePublisher _inner = new();
        public Task PublishAsync(string destinationPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            if (Path.GetFullPath(destinationPath).Equals(Path.GetFullPath(manifestPath), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Injected manifest publication failure.");
            return _inner.PublishAsync(destinationPath, content, cancellationToken);
        }
    }
}
