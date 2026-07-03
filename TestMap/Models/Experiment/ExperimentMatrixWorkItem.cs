using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Models.Experiment;

public class ExperimentMatrixWorkItem
{
    public int Id { get; set; }
    public int ExperimentRunId { get; set; }
    public int CandidateMethodId { get; set; }
    public int MemberId { get; set; }
    public string StableKey { get; set; } = string.Empty;
    public string Status { get; set; } = ExperimentMatrixWorkItemStatus.Pending;
    public AiProvider Provider { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public TestGenerationObjective Objective { get; set; } = TestGenerationObjective.TestSuiteExpansion;
    public TestGenerationApproach Approach { get; set; }
    public MetricsDrivenPath? MetricsPath { get; set; }
    public GenerationContextMode ContextMode { get; set; }
    public GenerationBudgetMode BudgetMode { get; set; }
    public string AblationVariantId { get; set; } = string.Empty;
    public string StepConfigJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public static class ExperimentMatrixWorkItemStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}
