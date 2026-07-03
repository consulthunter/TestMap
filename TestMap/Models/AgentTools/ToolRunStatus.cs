namespace TestMap.Models.AgentTools;

public enum ToolRunStatus
{
    Planned,
    Prepared,
    Running,
    Collected,
    Completed,
    CompletedNoChange,
    TimedOut,
    ToolCrashed,
    InvalidPatch,
    Skipped
}
