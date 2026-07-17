using TestMap.Models.Targets;
using TestMap.Services.Configuration;
using TestMap.Services.ProjectDiscovery;
using TestMap.Services.ProjectDiscovery.Contracts;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.IntegrationTests.ProjectDiscovery;

public sealed class ProjectCheckBundleIntegrationTests
{
    [Fact]
    public async Task TwoPinnedRevisions_PublishDistinctReusableClassifications()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-check-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var csv = Path.Combine(directory, "targets.csv");
            var manifestPath = Path.Combine(directory, "targets.yaml");
            await File.WriteAllTextAsync(csv,
                "name,lastCommitSHA\n" +
                "owner/repository,0123456789abcdef0123456789abcdef01234567\n" +
                "owner/repository,89abcdef0123456789abcdef0123456789abcdef\n");
            var source = await TestMap.Program.CreateTargetManifestAsync(csv, manifestPath, TargetDelimiter.Comma);
            var firstCommit = source.Manifest.Targets[0].Commit;
            var result = await TestMap.Program.CheckProjectsAsync(
                manifestPath,
                Path.Combine(directory, "project-check.yaml"),
                2,
                probe: new RevisionProbe(firstCommit));

            var fingerprint = new TargetFingerprintService();
            var reader = new TargetSourceReader(new TargetManifestSerializer(fingerprint), fingerprint);
            var positive = await reader.ReadAsync(result.TestsDetectedPath, TargetSourceMode.PinnedManifest);
            var negative = await reader.ReadAsync(result.NoTestsDetectedPath, TargetSourceMode.PinnedManifest);

            Assert.Single(positive.Targets);
            Assert.Single(negative.Targets);
            Assert.NotEqual(positive.Targets[0].Commit, negative.Targets[0].Commit);
            Assert.Equal(firstCommit, negative.Targets[0].Commit);
            Assert.Equal(2, result.Report.Observations.Select(row => row.RequestedCommit).Distinct().Count());
            Assert.All(result.Report.Observations, row => Assert.Equal(row.RequestedCommit, row.ObservedCommit));
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class RevisionProbe(string noTestsCommit) : IProjectCheckProbe
    {
        public Task<ProjectCheckObservation> CheckAsync(RepositoryTarget target, CancellationToken cancellationToken = default)
        {
            var hasTests = target.Commit != noTestsCommit;
            return Task.FromResult(new ProjectCheckObservation(
                target.TargetId,
                target.Repository,
                target.Commit,
                target.Commit,
                hasTests ? ProjectCheckStatus.TestsDetected : ProjectCheckStatus.NoTestsDetected,
                ProjectCheckPolicy.Name,
                ProjectCheckPolicy.Version,
                hasTests ? ProjectCheckEvidenceCategory.TestProjectFile : null,
                hasTests ? "Repository.Tests/Repository.Tests.csproj" : null,
                true,
                null,
                hasTests ? "Recognized test evidence was detected." : "No recognized test evidence was detected.",
                DateTimeOffset.UtcNow));
        }
    }
}
