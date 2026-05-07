namespace TestMap.Models.Experiment;

/// <summary>
/// Represents an individual step in the decomposed test generation process.
/// </summary>
public class GenerationStep
{
    public int Id { get; set; }
    public int GenerationAttemptId { get; set; }
    public GenerationStepType StepType { get; set; }
    public GenerationStepStatus Status { get; set; } = GenerationStepStatus.Executed;
    public string? SkipReason { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public double DurationSeconds { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResponseFormat { get; set; }
    public string? StructuredResponseJson { get; set; }
    public string? PromptVersion { get; set; }
    public string? ValidationStatus { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public string RuleDecisionJson { get; set; } = string.Empty;

    public virtual GenerationAttempt? GenerationAttempt { get; set; }
}
