using TestMap.Services.Configuration;
using TestMap.Models.Targets;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.UnitTests.Configuration;

public sealed class TargetSourceReaderTests
{
    [Fact]
    public async Task ReadAsync_LegacyList_AllowedOnlyForDiscovery()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "https://github.com/owner/repository.git\n");
        try
        {
            var fingerprint = new TargetFingerprintService();
            var reader = new TargetSourceReader(new TargetManifestSerializer(fingerprint), fingerprint);
            var source = await reader.ReadAsync(path, TargetSourceMode.Discovery);
            Assert.True(source.IsLegacy);
            Assert.Single(source.LegacyUrls);
            await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(path, TargetSourceMode.MeasuredExperiment));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReadAsync_ManifestWithChangedRejectionReport_IsRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "input.csv");
        var manifest = Path.Combine(directory, "targets.yaml");
        await File.WriteAllTextAsync(input, "name,lastCommitSHA\nowner/repository,0123456789abcdef0123456789abcdef01234567\n");
        try
        {
            var created = await TestMap.Program.CreateTargetManifestAsync(input, manifest, TargetDelimiter.Comma);
            await File.AppendAllTextAsync(created.RejectionReportPath, "changed");
            var fingerprint = new TargetFingerprintService();
            var reader = new TargetSourceReader(new TargetManifestSerializer(fingerprint), fingerprint);
            await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(manifest, TargetSourceMode.MeasuredExperiment));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task ReadAsync_Schema3WithHashValidButMismatchedResolution_IsRejectedOffline()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-url-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "target.yaml");
        try
        {
            var created = await TestMap.Program.CreateSingleRepositoryTargetAsync(
                "https://github.com/owner/repository", manifestPath,
                resolutionClient: new StableClient());
            var fingerprint = new TargetFingerprintService();
            var resolutionSerializer = new SingleRepositoryResolutionSerializer(
                new SingleRepositoryResolutionValidator(fingerprint));
            var resolutionBytes = await File.ReadAllBytesAsync(created.ResolutionPath);
            var resolution = resolutionSerializer.Deserialize(resolutionBytes) with
            {
                ResolvedCommit = "89abcdef0123456789abcdef0123456789abcdef"
            };
            var changedResolutionBytes = resolutionSerializer.Serialize(resolution);
            await File.WriteAllBytesAsync(created.ResolutionPath, changedResolutionBytes);

            var manifestSerializer = new TargetManifestSerializer(fingerprint);
            var (manifest, _) = await manifestSerializer.ReadAsync(manifestPath);
            manifest = manifest with
            {
                Resolution = manifest.Resolution! with
                {
                    Sha256 = fingerprint.ComputeSha256(changedResolutionBytes)
                }
            };
            await File.WriteAllBytesAsync(manifestPath, manifestSerializer.Serialize(manifest));
            var reader = new TargetSourceReader(manifestSerializer, fingerprint);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                reader.ReadAsync(manifestPath, TargetSourceMode.MeasuredExperiment));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task ReadAsync_Schema3WithMissingResolution_IsRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-url-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "target.yaml");
        try
        {
            var created = await TestMap.Program.CreateSingleRepositoryTargetAsync(
                "https://github.com/owner/repository", manifestPath,
                resolutionClient: new StableClient());
            File.Delete(created.ResolutionPath);
            var fingerprint = new TargetFingerprintService();
            var reader = new TargetSourceReader(new TargetManifestSerializer(fingerprint), fingerprint);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                reader.ReadAsync(manifestPath, TargetSourceMode.PinnedManifest));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class StableClient : IRepositoryResolutionClient
    {
        private const string Commit = "0123456789abcdef0123456789abcdef01234567";
        public Task<RepositoryResolutionMetadata> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryResolutionMetadata(owner + "/" + repository, "main"));
        public Task<RepositoryResolutionBranch> GetBranchAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryResolutionBranch(branch, Commit));
        public Task<RepositoryResolutionCommit> GetCommitAsync(string owner, string repository, string commit, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryResolutionCommit(Commit));
    }
}
