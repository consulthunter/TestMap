using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;
using TestMap.UnitTests.Targets;

namespace TestMap.UnitTests.ProjectDiscovery;

public sealed class ProjectCheckBundlePublisherTests
{
    [Fact]
    public void ReportBuilder_AcceptsSchema3InputAndRecordsItsSchema()
    {
        var target = TargetTestData.Target();
        var manifest = new TargetManifest(
            3,
            new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
            new TargetSourceProvenance(
                null, TargetTestData.Hash, "github_repository_url",
                "https://github.com/owner/repository"),
            null,
            [target],
            null,
            new TargetReportReference("resolution.yaml", TargetTestData.Hash));

        var report = new ProjectCheckReportBuilder(new ProjectCheckContractValidator()).Build(
            manifest, "target.yaml", TargetTestData.Hash, [Observation(target)]);

        Assert.Equal(3, report.Input.SchemaVersion);
    }

    [Fact]
    public async Task PublishAsync_PointerFailurePreservesPreviousCompletedPointer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-bundle-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var inputPath = Path.Combine(directory, "targets.yaml");
            var bundlePath = Path.Combine(directory, "project-check.yaml");
            await File.WriteAllTextAsync(bundlePath, "previous-complete-bundle\n");
            var target = TargetTestData.Target();
            var manifest = TargetTestData.Manifest(target);
            var validator = new ProjectCheckContractValidator();
            var report = new ProjectCheckReportBuilder(validator).Build(
                manifest, inputPath, TargetTestData.Hash,
                [Observation(target)]);
            var fingerprint = new TargetFingerprintService();
            var sut = new ProjectCheckBundlePublisher(
                new ProjectCheckSerializer(validator),
                new TargetManifestSerializer(fingerprint),
                new ProjectCheckPartitionService(validator),
                validator,
                new ProjectCheckOutputPathResolver(),
                fingerprint,
                new FailingPointerPublisher(bundlePath));

            await Assert.ThrowsAsync<IOException>(() => sut.PublishAsync(
                inputPath, TargetTestData.Hash, manifest, report, bundlePath));

            Assert.Equal("previous-complete-bundle\n", await File.ReadAllTextAsync(bundlePath));
            Assert.Equal(4, Directory.EnumerateFiles(directory, "*.yaml").Count());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void OutputPathResolver_RejectsInputOverwrite()
    {
        Assert.Throws<InvalidDataException>(() =>
            new ProjectCheckOutputPathResolver().Resolve("targets.yaml", "targets.yaml"));
    }

    [Theory]
    [InlineData("Authorization: Bearer ghp_abcdefghijkl")]
    [InlineData("token=github_pat_abcdef")]
    [InlineData("api_key=super-secret")]
    public void Sanitizer_RedactsCredentialShapedText(string text)
    {
        var result = new ProjectCheckSanitizer().Summary(text);
        Assert.Contains("[redacted]", result);
        Assert.DoesNotContain("secret", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ghp_", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github_pat_", result, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectCheckObservation Observation(RepositoryTarget target) => new(
        target.TargetId, target.Repository, target.Commit, target.Commit,
        ProjectCheckStatus.NoTestsDetected, ProjectCheckPolicy.Name, ProjectCheckPolicy.Version,
        null, null, true, null, "No recognized test evidence was detected.", DateTimeOffset.UtcNow);

    private sealed class FailingPointerPublisher(string pointerPath) : IAtomicFilePublisher
    {
        private readonly AtomicFilePublisher _inner = new();

        public Task PublishAsync(string destinationPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            if (Path.GetFullPath(destinationPath).Equals(Path.GetFullPath(pointerPath), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Injected pointer failure.");
            return _inner.PublishAsync(destinationPath, content, cancellationToken);
        }
    }
}
