using TestMap.Models.Targets;

namespace TestMap.Services.ProjectDiscovery;

public sealed record ProjectCheckPartition(TargetManifest TestsDetected, TargetManifest NoTestsDetected);

public sealed class ProjectCheckPartitionService(ProjectCheckContractValidator validator)
{
    public ProjectCheckPartition Partition(
        TargetManifest input,
        string inputFile,
        string inputSha256,
        ProjectCheckReport report,
        TargetReportReference reportReference,
        DateTimeOffset? generatedAtUtc = null)
    {
        validator.ValidateReport(report, input);
        var observations = report.Observations.ToDictionary(row => row.TargetId, StringComparer.Ordinal);
        var positive = input.Targets.Where(target => observations[target.TargetId].Status == ProjectCheckStatus.TestsDetected).ToArray();
        var negative = input.Targets.Where(target => observations[target.TargetId].Status == ProjectCheckStatus.NoTestsDetected).ToArray();
        var timestamp = (generatedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new ProjectCheckPartition(
            Derived("tests_detected", positive),
            Derived("no_tests_detected", negative));

        TargetManifest Derived(string category, IReadOnlyList<RepositoryTarget> targets) => new(
            2,
            timestamp,
            new TargetSourceProvenance(Path.GetFileName(inputFile), inputSha256),
            null,
            targets,
            new TargetDerivationProvenance(
                "project_test_presence", category, ProjectCheckPolicy.Name, ProjectCheckPolicy.Version, reportReference));
    }
}
