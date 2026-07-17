namespace TestMap.Models.Targets;

public enum IntegrityCheckpoint
{
    PreExtraction,
    PreBaseline,
    ExperimentStart,
    PreAttempt,
    PostAttemptPreAnalysis,
    PostRollback,
    PreResultsPublication
}

public enum WorkspaceIntegrityStatus
{
    VerifiedClean,
    VerifiedExpectedChanges,
    RevisionMismatch,
    OriginMismatch,
    UnexpectedChanges,
    RepositoryInvalid,
    StatusUnavailable,
    RestoreFailed
}

public sealed record WorkspaceIntegrityObservation(
    int ProjectId,
    int? ExperimentRunId,
    string TargetId,
    string? ProducerLane,
    string? WorkItemStableKey,
    int? AttemptNumber,
    IntegrityCheckpoint Checkpoint,
    string ExpectedCommit,
    string? ActualCommit,
    bool OriginMatches,
    bool? WorkingTreeDirty,
    WorkspaceIntegrityStatus Status,
    string Details,
    DateTimeOffset ObservedAtUtc);

public static class WorkspaceIntegrityStatusExtensions
{
    public static bool IsVerified(this WorkspaceIntegrityStatus status) =>
        status is WorkspaceIntegrityStatus.VerifiedClean or WorkspaceIntegrityStatus.VerifiedExpectedChanges;
}
