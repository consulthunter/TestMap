using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Models.Experiment;

/// <summary>
/// Represents a single attempt to generate a test using a specific provider and strategy.
/// For pass@5, there will be 5 attempts. For repair@5, up to 5 attempts with repairs.
/// </summary>
public class GenerationAttempt
{
    public int Id { get; set; }
    public int CandidateMethodId { get; set; }
    public int? ExperimentMatrixWorkItemId { get; set; }
    public AiProvider Provider { get; set; }
    public string? ModelName { get; set; }
    public TestGenerationObjective Objective { get; set; } = TestGenerationObjective.TestSuiteExpansion;
    public TestGenerationApproach GenerationApproach { get; set; } = TestGenerationApproach.MetricsDriven;
    public MetricsDrivenPath? MetricsPath { get; set; }
    public GenerationContextMode ContextMode { get; set; } = GenerationContextMode.ChainedHistory;
    public GenerationBudgetMode BudgetMode { get; set; } = GenerationBudgetMode.PassAt1;
    public string AblationVariantId { get; set; } = string.Empty;
    public string StepConfigJson { get; set; } = string.Empty;
    public string EffectiveProfileJson { get; set; } = string.Empty;
    public string EffectiveProfileHash { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int AttemptNumber { get; set; }
    public bool IsRepairAttempt { get; set; }
    public int? ParentAttemptId { get; set; }
    public int? ParentAttemptNumber { get; set; }
    public string RuleDecisionSnapshotJson { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalTokensUsed { get; set; }
    public double GenerationDurationSeconds { get; set; }
    public double ValidationDurationSeconds { get; set; }
    public double TotalDurationSeconds { get; set; }
    public string Status { get; set; } = string.Empty;
    public TestFailureKind FailureKind { get; set; } = TestFailureKind.None;
    public string? FailureStage { get; set; }
    public string? FailureCategory { get; set; }
    public string? ErrorMessage { get; set; }

    // -----------------------------------------------------------------------
    // Basic Extension patch metadata — written after patch application,
    // persisted to generation_attempts via GenerationAttemptEntity.
    // Null for non-Basic-Extension generation approaches.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Raw JSON string of the <c>BasicExtensionPatch</c> that was generated (generation attempt).
    /// Also set for legacy method-only responses so the raw output is always captured.
    /// </summary>
    public string? PatchJson { get; set; }

    /// <summary>
    /// Raw JSON string of the corrected <c>BasicExtensionPatch</c> returned by the repair step.
    /// Only set on repair attempts.
    /// </summary>
    public string? RepairPatchJson { get; set; }

    /// <summary>
    /// Machine-readable outcome of patch application, e.g. <c>"Success"</c>,
    /// <c>"DuplicateTestMethod"</c>, <c>"MalformedTestMethod"</c>, <c>"TargetClassMissing"</c>.
    /// Null when the Basic Extension patch path was not taken.
    /// </summary>
    public string? PatchApplicationOutcome { get; set; }

    /// <summary>Number of new <c>using</c> directives added by the patch applier.</summary>
    public int? AppliedUsingCount { get; set; }

    /// <summary>Number of helper methods added by the patch applier.</summary>
    public int? AppliedHelperCount { get; set; }

    /// <summary>Absolute path of the test file snapshot produced by this attempt.</summary>
    public string? ModifiedFilePath { get; set; }

    /// <summary>
    /// Exact test file contents after patch application and before workspace rollback.
    /// </summary>
    public string? ModifiedFileContents { get; set; }

    /// <summary>
    /// Lowercase SHA-256 of the UTF-8 representation of <see cref="ModifiedFileContents"/>.
    /// </summary>
    public string? ModifiedFileSha256 { get; set; }

    // -----------------------------------------------------------------------
    // Transient fields — computed at run time, not persisted to the database.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Conversation transcript produced by the generation/repair pipeline for this attempt.
    /// Not persisted. Passed into the next repair attempt when
    /// <see cref="GenerationContextMode.ChainedHistory"/> is active so that the model sees
    /// the full prior exchange rather than just the previous patch and its errors.
    /// </summary>
    public string? ConversationTranscript { get; set; }

    /// <summary>
    /// Running sum of <see cref="TotalTokensUsed"/> across all attempts in this repair chain
    /// (initial generation + all preceding repairs + this attempt).
    /// Not persisted; computed at write time by the orchestration layer.
    /// For <see cref="GenerationBudgetMode.PassAt1RepairAt5"/> this counts the full cost
    /// of reaching this repair stage. For independent attempts (PassAt1, PassAt5) it equals
    /// <see cref="TotalTokensUsed"/>.
    /// </summary>
    public int ChainCumulativeTokensUsed { get; set; }

    public virtual CandidateMethod? CandidateMethod { get; set; }
    public virtual ICollection<GenerationStep> GenerationSteps { get; set; } = new List<GenerationStep>();
    public virtual TestExecution? TestExecution { get; set; }
}
