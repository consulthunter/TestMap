using TestMap.Models.Targets;

namespace TestMap.Services.ProjectDiscovery;

public sealed class ProjectCheckReportBuilder(ProjectCheckContractValidator validator)
{
    public ProjectCheckReport Build(
        TargetManifest input,
        string inputFile,
        string inputSha256,
        IReadOnlyList<ProjectCheckObservation> observations,
        DateTimeOffset? generatedAtUtc = null)
    {
        var byStatus = Enum.GetValues<ProjectCheckStatus>()
            .ToDictionary(status => status, status => observations.Count(row => row.Status == status));
        var report = new ProjectCheckReport(
            1,
            (generatedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            new ProjectCheckInputReference(Path.GetFileName(inputFile), inputSha256, input.SchemaVersion),
            new ProjectCheckPolicyReference(ProjectCheckPolicy.Name, ProjectCheckPolicy.Version),
            new ProjectCheckSummary(
                observations.Count,
                byStatus[ProjectCheckStatus.TestsDetected],
                byStatus[ProjectCheckStatus.NoTestsDetected],
                observations.Count - byStatus[ProjectCheckStatus.TestsDetected] - byStatus[ProjectCheckStatus.NoTestsDetected],
                byStatus),
            observations);
        validator.ValidateReport(report, input);
        return report;
    }
}
