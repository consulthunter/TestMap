using System.ComponentModel.DataAnnotations;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Entities.AgentTools;

public class ToolAttemptEntity
{
    public int Id { get; set; }
    public int ExperimentRunId { get; set; }
    public int? MatrixWorkItemId { get; set; }
    public int CandidateMethodId { get; set; }
    public int? TargetedBaselineId { get; set; }
    public int? PostAttemptTestRunId { get; set; }
    [MaxLength(64)]  public string EffectiveProfileHash { get; set; } = string.Empty;
    [MaxLength(100)] public string ToolId { get; set; } = string.Empty;
    [MaxLength(50)]  public string RunStatus { get; set; } = string.Empty;
    [MaxLength(50)]  public string ValidationOutcome { get; set; } = string.Empty;
    [MaxLength(50)]  public string ObservedOutcome { get; set; } = string.Empty;
    [MaxLength(300)] public string ImageName { get; set; } = string.Empty;
    [MaxLength(100)] public string ImageKey { get; set; } = string.Empty;
    [MaxLength(100)] public string BaseCommit { get; set; } = string.Empty;
    public string WorkspacePath { get; set; } = string.Empty;
    public string ArtifactPath { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double ElapsedSeconds { get; set; }
    public int TimeoutSeconds { get; set; }
    public int? ExitCode { get; set; }
    [MaxLength(200)] public string ToolVersion { get; set; } = string.Empty;
    [MaxLength(200)] public string Model { get; set; } = string.Empty;
    [MaxLength(100)] public string ProviderId { get; set; } = string.Empty;
    public bool JsonlLogAvailable { get; set; }
    public bool UsageAvailable { get; set; }
    [MaxLength(50)] public string UsageSource { get; set; } = string.Empty;
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

    public virtual ExperimentRunEntity? ExperimentRun { get; set; }
    public virtual ExperimentMatrixWorkItemEntity? MatrixWorkItem { get; set; }
    public virtual CandidateMethodEntity? CandidateMethod { get; set; }
}
