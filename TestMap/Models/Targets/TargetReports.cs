namespace TestMap.Models.Targets;

public enum RejectionKind
{
    MissingName,
    InvalidRepositoryName,
    MissingCommit,
    InvalidCommit,
    MalformedRecord
}

public sealed record TargetRejectionRecord(
    string ReportSchemaVersion,
    string SourceFile,
    long SourceRow,
    string? RawName,
    string? RawCommit,
    RejectionKind RejectionKind,
    string Summary);

public enum TargetVerificationStatus
{
    Available,
    InvalidTarget,
    RepositoryUnavailable,
    AuthenticationFailed,
    RateLimited,
    CommitUnavailable,
    VerificationFailed
}

public sealed record TargetVerificationRecord(
    string ReportSchemaVersion,
    string ManifestSha256,
    string TargetId,
    string Repository,
    string RequestedCommit,
    string? ResolvedCommit,
    TargetVerificationStatus Status,
    DateTimeOffset VerifiedAtUtc,
    string Summary);

public enum TargetExecutionStatus
{
    Pending,
    Materialized,
    Completed,
    InvalidTarget,
    RepositoryUnavailable,
    AuthenticationFailed,
    RateLimited,
    CommitUnavailable,
    WorkspaceBusy,
    MaterializationFailed,
    PipelineFailed,
    IntegrityFailed
}

public sealed record TargetExecutionRecord(
    string ReportSchemaVersion,
    string ManifestSha256,
    string TargetId,
    string Repository,
    string RequestedCommit,
    string? ResolvedCommit,
    TargetExecutionStatus Status,
    string? FailureStage,
    string? FailureKind,
    string Summary,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);
