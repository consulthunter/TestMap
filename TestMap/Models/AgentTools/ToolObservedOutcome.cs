namespace TestMap.Models.AgentTools;

public enum ToolObservedOutcome
{
    NotEvaluated,
    Skipped,
    ToolFailed,
    TimedOut,
    NoChange,
    ChangedNotValidated,
    ValidatedEvidencePositive,
    ValidatedLowImpact,
    FailedEvidencePositive,
    ValidationFailed
}
