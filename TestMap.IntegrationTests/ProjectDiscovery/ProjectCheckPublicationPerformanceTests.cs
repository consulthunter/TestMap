using System.Diagnostics;
using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery;
using TestMap.Services.Targets;
using Xunit.Abstractions;

namespace TestMap.IntegrationTests.ProjectDiscovery;

public sealed class ProjectCheckPublicationPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task TenThousandPrecomputedObservations_PublishWithinReferenceBudget()
    {
        const int targetCount = 10_000;
        var directory = Path.Combine(Path.GetTempPath(), "testmap-check-scale-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var identity = new TargetIdentityService();
            var targets = Enumerable.Range(1, targetCount)
                .Select(index => identity.Create(
                    $"owner/repository-{index:D5}", index.ToString("x40"), [index]))
                .ToArray();
            var hash = new string('a', 64);
            var manifest = new TargetManifest(
                1, DateTimeOffset.UtcNow, new TargetSourceProvenance("targets.csv", hash),
                new TargetReportReference("rejections.csv", hash), targets);
            var observations = targets.Select(target => new ProjectCheckObservation(
                target.TargetId, target.Repository, target.Commit, target.Commit,
                ProjectCheckStatus.NoTestsDetected, ProjectCheckPolicy.Name, ProjectCheckPolicy.Version,
                null, null, true, null, "No recognized test evidence was detected.", DateTimeOffset.UtcNow)).ToArray();
            var validator = new ProjectCheckContractValidator();
            var report = new ProjectCheckReportBuilder(validator).Build(
                manifest, "targets.yaml", hash, observations);
            var fingerprint = new TargetFingerprintService();
            var publisher = new ProjectCheckBundlePublisher(
                new ProjectCheckSerializer(validator),
                new TargetManifestSerializer(fingerprint),
                new ProjectCheckPartitionService(validator),
                validator,
                new ProjectCheckOutputPathResolver(),
                fingerprint,
                new AtomicFilePublisher());

            var stopwatch = Stopwatch.StartNew();
            var result = await publisher.PublishAsync(
                Path.Combine(directory, "targets.yaml"), hash, manifest, report,
                Path.Combine(directory, "project-check.yaml"));
            stopwatch.Stop();

            output.WriteLine(
                $"targets={targetCount}; elapsed_ms={stopwatch.ElapsedMilliseconds}; " +
                $"runtime={Environment.Version}; os={Environment.OSVersion}; directory={directory}; " +
                $"report_bytes={new FileInfo(result.ReportPath).Length}");
            Assert.Equal(targetCount, result.Bundle.Artifacts.Report.Records);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30),
                $"Publication took {stopwatch.Elapsed}, exceeding the 30-second reference budget.");
        }
        finally { Directory.Delete(directory, true); }
    }
}
