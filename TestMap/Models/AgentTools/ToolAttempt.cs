namespace TestMap.Models.AgentTools;

public sealed class ToolAttempt
{
    public int Id { get; set; }
    public int ExperimentRunId { get; set; }
    public int? MatrixWorkItemId { get; set; }
    public int CandidateMethodId { get; set; }
    public int? TargetedBaselineId { get; set; }

    /// <summary>
    /// Test run id from the post-attempt build/test measurement on the modified workspace.
    /// Null when measurement was not performed or was skipped.
    /// </summary>
    public int? PostAttemptTestRunId { get; set; }
    public string EffectiveProfileHash { get; set; } = string.Empty;
    public string ToolId { get; set; } = string.Empty;
    public ToolRunStatus RunStatus { get; set; } = ToolRunStatus.Planned;
    public ToolValidationOutcome ValidationOutcome { get; set; } = ToolValidationOutcome.NotEvaluated;
    public ToolObservedOutcome ObservedOutcome { get; set; } = ToolObservedOutcome.NotEvaluated;
    public string ImageName { get; set; } = string.Empty;
    public string ImageKey { get; set; } = string.Empty;
    public string BaseCommit { get; set; } = string.Empty;
    public string WorkspacePath { get; set; } = string.Empty;
    public string ArtifactPath { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double ElapsedSeconds { get; set; }
    public int TimeoutSeconds { get; set; }
    public int? ExitCode { get; set; }
    public string ToolVersion { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public bool JsonlLogAvailable { get; set; }
    public bool UsageAvailable { get; set; }
    public string UsageSource { get; set; } = string.Empty;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? EstimatedPromptTokens { get; set; }
    public int ChangedFilesCount { get; set; }
    public int ProductionFilesChanged { get; set; }
    public int TestFilesChanged { get; set; }
    public int ProjectFilesChanged { get; set; }
    public int DeletedFilesCount { get; set; }
    public string ConstraintViolationSummary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
