namespace TestMap.Models.Targets;

public enum ProjectCheckStatus
{
    TestsDetected,
    NoTestsDetected,
    InvalidTarget,
    RepositoryIdentityMismatch,
    RepositoryUnavailable,
    AuthenticationFailed,
    AuthorizationFailed,
    RateLimited,
    CommitUnavailable,
    CommitMismatch,
    TreeUnavailable,
    TreeTruncated,
    ServiceUnavailable,
    CheckFailed
}

public enum ProjectCheckEvidenceCategory
{
    TestDirectory,
    TestProjectFile,
    TestSourceFile
}

public static class ProjectCheckPolicy
{
    public const string Name = "project-test-presence";
    public const string Version = "1";
    public const string Identifier = Name + "/v" + Version;
}

public sealed record ProjectCheckEvidence(ProjectCheckEvidenceCategory Category, string Path);

public sealed record ProjectCheckObservation(
    string TargetId,
    string Repository,
    string RequestedCommit,
    string? ObservedCommit,
    ProjectCheckStatus Status,
    string PolicyName,
    string PolicyVersion,
    ProjectCheckEvidenceCategory? EvidenceCategory,
    string? EvidencePath,
    bool? TreeComplete,
    string? ReasonKind,
    string Summary,
    DateTimeOffset CheckedAtUtc);

public sealed record ProjectCheckInputReference(string File, string Sha256, int SchemaVersion);

public sealed record ProjectCheckPolicyReference(string Name, string Version);

public sealed record ProjectCheckSummary(
    int InputTargets,
    int TestsDetected,
    int NoTestsDetected,
    int Indeterminate,
    IReadOnlyDictionary<ProjectCheckStatus, int> ByStatus);

public sealed record ProjectCheckReport(
    int ReportSchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    ProjectCheckInputReference Input,
    ProjectCheckPolicyReference Policy,
    ProjectCheckSummary Summary,
    IReadOnlyList<ProjectCheckObservation> Observations);

public sealed record ProjectCheckArtifactReference(string File, string Sha256, int Records);

public sealed record ProjectCheckArtifacts(
    ProjectCheckArtifactReference Report,
    ProjectCheckArtifactReference TestsDetected,
    ProjectCheckArtifactReference NoTestsDetected);

public sealed record ProjectCheckBundle(
    int BundleSchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    ProjectCheckInputReference Input,
    ProjectCheckPolicyReference Policy,
    bool Complete,
    bool ClassificationComplete,
    ProjectCheckArtifacts Artifacts);

public sealed record ProjectCheckPublicationResult(
    ProjectCheckBundle Bundle,
    string BundlePath,
    string ReportPath,
    string TestsDetectedPath,
    string NoTestsDetectedPath,
    ProjectCheckReport Report);
