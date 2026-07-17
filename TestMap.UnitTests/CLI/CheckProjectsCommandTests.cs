using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery;
using TestMap.Services.ProjectDiscovery.Contracts;
using TestMap.Services.Configuration;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.UnitTests.CLI;

public sealed class CheckProjectsCommandTests
{
    [Fact]
    public async Task CheckProjectsAsync_PublishesReloadableCompleteBundle()
    {
        var directory = CreateTempDirectory();
        try
        {
            var (manifestPath, targets) = await CreateManifestAsync(directory);
            var output = Path.Combine(directory, "screening.yaml");
            var result = await TestMap.Program.CheckProjectsAsync(
                manifestPath, output, 2, probe: new StubProbe(target =>
                    target.TargetId == targets[0].TargetId
                        ? Observation(target, ProjectCheckStatus.TestsDetected, "tests/WidgetTests.cs")
                        : Observation(target, ProjectCheckStatus.NoTestsDetected)));

            Assert.True(result.Bundle.ClassificationComplete);
            Assert.Equal(2, result.Report.Summary.InputTargets);
            Assert.Equal(1, result.Report.Summary.TestsDetected);
            Assert.Equal(1, result.Report.Summary.NoTestsDetected);
            Assert.True(File.Exists(output));

            var fingerprint = new TargetFingerprintService();
            var validator = new ProjectCheckContractValidator();
            var loaded = await new ProjectCheckBundleReader(
                    new ProjectCheckSerializer(validator),
                    new TargetManifestSerializer(fingerprint),
                    validator,
                    fingerprint)
                .ReadAsync(output);
            Assert.Single(loaded.TestsDetected.Targets);
            Assert.Single(loaded.NoTestsDetected.Targets);
            AssertTargetEqual(targets[0], loaded.TestsDetected.Targets[0]);
            AssertTargetEqual(targets[1], loaded.NoTestsDetected.Targets[0]);
            var downstream = await new TargetSourceReader(
                    new TargetManifestSerializer(fingerprint), fingerprint)
                .ReadAsync(result.TestsDetectedPath, TargetSourceMode.MeasuredExperiment);
            Assert.Single(downstream.Targets);
            Assert.Equal(targets[0].Commit, downstream.Targets[0].Commit);

            var reportYaml = await File.ReadAllTextAsync(result.ReportPath);
            var bundleYaml = await File.ReadAllTextAsync(result.BundlePath);
            Assert.Contains("report_schema_version: 1", reportYaml);
            Assert.Contains("requested_commit:", reportYaml);
            Assert.Contains("bundle_schema_version: 1", bundleYaml);
            Assert.Contains("classification_complete: true", bundleYaml);

            File.Delete(result.ReportPath);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new TargetSourceReader(new TargetManifestSerializer(fingerprint), fingerprint)
                    .ReadAsync(result.TestsDetectedPath, TargetSourceMode.PinnedManifest));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CheckProjectsAsync_IndeterminateTargetIsNotCategorized()
    {
        var directory = CreateTempDirectory();
        try
        {
            var (manifestPath, _) = await CreateManifestAsync(directory);
            var result = await TestMap.Program.CheckProjectsAsync(
                manifestPath, null, 2, probe: new StubProbe(target => new ProjectCheckObservation(
                    target.TargetId, target.Repository, target.Commit, null,
                    ProjectCheckStatus.RateLimited, ProjectCheckPolicy.Name, ProjectCheckPolicy.Version,
                    null, null, null, "rate_limited", "Provider rate limit was exceeded.", DateTimeOffset.UtcNow)));

            Assert.False(result.Bundle.ClassificationComplete);
            Assert.Equal(2, result.Report.Summary.Indeterminate);
            var positive = new TargetManifestSerializer(new TargetFingerprintService())
                .Deserialize(await File.ReadAllBytesAsync(result.TestsDetectedPath));
            var negative = new TargetManifestSerializer(new TargetFingerprintService())
                .Deserialize(await File.ReadAllBytesAsync(result.NoTestsDetectedPath));
            Assert.Empty(positive.Targets);
            Assert.Empty(negative.Targets);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CheckProjectsAsync_LegacyListIsRejectedBeforeProbe()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "targets.txt");
            await File.WriteAllTextAsync(path, "https://github.com/owner/repository.git");
            var probe = new CountingProbe();
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                TestMap.Program.CheckProjectsAsync(path, probe: probe));
            Assert.Equal(0, probe.Calls);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task<(string Path, IReadOnlyList<RepositoryTarget> Targets)> CreateManifestAsync(string directory)
    {
        var input = Path.Combine(directory, "targets.csv");
        var output = Path.Combine(directory, "targets.yaml");
        await File.WriteAllTextAsync(input,
            "name,lastCommitSHA\n" +
            "owner/first,0123456789abcdef0123456789abcdef01234567\n" +
            "owner/second,89abcdef0123456789abcdef0123456789abcdef\n");
        var created = await TestMap.Program.CreateTargetManifestAsync(input, output, TargetDelimiter.Comma);
        return (output, created.Manifest.Targets);
    }

    private static ProjectCheckObservation Observation(
        RepositoryTarget target,
        ProjectCheckStatus status,
        string? evidencePath = null) => new(
        target.TargetId, target.Repository, target.Commit, target.Commit, status,
        ProjectCheckPolicy.Name, ProjectCheckPolicy.Version,
        evidencePath is null ? null : ProjectCheckEvidenceCategory.TestDirectory,
        evidencePath, true, null, "complete", DateTimeOffset.UtcNow);

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "testmap-project-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void AssertTargetEqual(RepositoryTarget expected, RepositoryTarget actual)
    {
        Assert.Equal(expected.TargetId, actual.TargetId);
        Assert.Equal(expected.Repository, actual.Repository);
        Assert.Equal(expected.Url, actual.Url);
        Assert.Equal(expected.Commit, actual.Commit);
        Assert.Equal(expected.SourceRows, actual.SourceRows);
    }

    private sealed class StubProbe(Func<RepositoryTarget, ProjectCheckObservation> factory) : IProjectCheckProbe
    {
        public Task<ProjectCheckObservation> CheckAsync(RepositoryTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult(factory(target));
    }

    private sealed class CountingProbe : IProjectCheckProbe
    {
        public int Calls { get; private set; }
        public Task<ProjectCheckObservation> CheckAsync(RepositoryTarget target, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new NotSupportedException();
        }
    }
}
