namespace TestMap.Models.AgentTools;

public enum ToolValidationOutcome
{
    NotEvaluated,
    Skipped,
    TimedOut,
    ToolFailed,
    BuildFailed,
    TestsFailed,
    Passed,
    ConstraintViolation
}
