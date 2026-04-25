using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Models.Experiment;

/// <summary>
/// Represents a single attempt to generate a test using a specific provider and strategy.
/// For pass@5, there will be 5 attempts. For repair@5, up to 5 attempts with repairs.
/// </summary>
public class GenerationAttempt
{
    public int Id { get; set; }
    public int CandidateMethodId { get; set; }
    public AiProvider Provider { get; set; }
    public string? ModelName { get; set; }
    public GenerationStrategy Strategy { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalTokensUsed { get; set; }
    public double TotalDurationSeconds { get; set; }
    public string Status { get; set; } = string.Empty;
    public TestFailureKind FailureKind { get; set; } = TestFailureKind.None;
    public string? FailureStage { get; set; }
    public string? FailureCategory { get; set; }
    public string? ErrorMessage { get; set; }

    public virtual CandidateMethod? CandidateMethod { get; set; }
    public virtual ICollection<GenerationStep> GenerationSteps { get; set; } = new List<GenerationStep>();
    public virtual TestExecution? TestExecution { get; set; }
}