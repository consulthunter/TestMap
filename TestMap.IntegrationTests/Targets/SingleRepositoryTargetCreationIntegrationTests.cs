using TestMap.Services.Configuration;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.IntegrationTests.Targets;

public sealed class SingleRepositoryTargetCreationIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CheckedExample_IsAValidOfflineSchema3Bundle()
    {
        var manifestPath = FindRepositoryFile(
            "TestMap", "Data", "example.yaml");
        var fingerprint = new TargetFingerprintService();

        var source = await new TargetSourceReader(
                new TargetManifestSerializer(fingerprint), fingerprint)
            .ReadAsync(manifestPath, TargetSourceMode.PinnedManifest);

        Assert.Equal(3, source.Manifest?.SchemaVersion);
        Assert.Equal("consulthunter/testmap-example", Assert.Single(source.Targets).Repository);
        Assert.Equal("867ab17d3141bc0e6d696486bd8edb67546c00d1", source.Targets[0].Commit);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAndConsume_Schema3Bundle_RetainsExactRevisionWithoutProviderAccess()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-url-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var client = new StableClient();
            var manifestPath = Path.Combine(directory, "target.yaml");
            var created = await TestMap.Program.CreateSingleRepositoryTargetAsync(
                "https://github.com/OWNER/repository.git", manifestPath,
                resolutionClient: client);
            var callsAfterCreation = client.Calls;
            var fingerprint = new TargetFingerprintService();
            var source = await new TargetSourceReader(
                    new TargetManifestSerializer(fingerprint), fingerprint)
                .ReadAsync(manifestPath, TargetSourceMode.MeasuredExperiment);

            Assert.Equal(3, source.Manifest?.SchemaVersion);
            Assert.Equal(Commit, Assert.Single(source.Targets).Commit);
            Assert.Equal(callsAfterCreation, client.Calls);
            Assert.True(File.Exists(created.ResolutionPath));
        }
        finally { Directory.Delete(directory, true); }
    }

    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private sealed class StableClient : IRepositoryResolutionClient
    {
        public int Calls { get; private set; }
        public Task<RepositoryResolutionMetadata> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new RepositoryResolutionMetadata(owner + "/" + repository, "main")); }
        public Task<RepositoryResolutionBranch> GetBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new RepositoryResolutionBranch(branch, Commit)); }
        public Task<RepositoryResolutionCommit> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new RepositoryResolutionCommit(Commit)); }
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate repository fixture.");
    }
}
