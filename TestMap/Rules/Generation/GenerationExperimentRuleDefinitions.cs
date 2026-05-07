using TestMap.Models.Rules;

namespace TestMap.Rules.Generation;

public static class GenerationExperimentRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "GenerationExperiment";

    public static RuleDefinition HistoryComparisonExpanded { get; } = Define("generation.experiment.history-comparison-expanded", "History comparison expanded", "History comparison expanded to no-history and chained-history variants.");
    public static RuleDefinition SingleHistoryModeUsed { get; } = Define("generation.experiment.single-history-mode-used", "Single history mode used", "Configured context modes were used without expanding history comparison.");
    public static RuleDefinition StepAblationDisabled { get; } = Define("generation.experiment.step-ablation-disabled", "Step ablation disabled", "Step ablation is disabled and only baseline steps are used.");
    public static RuleDefinition StepAblationBaselineIncluded { get; } = Define("generation.experiment.step-ablation-baseline-included", "Step ablation baseline included", "The baseline step configuration was included.");
    public static RuleDefinition StepAblationAllDisabledExcluded { get; } = Define("generation.experiment.step-ablation-all-disabled-excluded", "All-disabled ablation excluded", "The all-disabled ablation combination was excluded.");
    public static RuleDefinition StepAblationCapped { get; } = Define("generation.experiment.step-ablation-capped", "Step ablation capped", "Step ablation variants were capped by configuration.");
    public static RuleDefinition StepAblationVariantCount { get; } = Define("generation.experiment.step-ablation-variant-count", "Step ablation variant count", "Final step ablation variant count was recorded.");
    public static RuleDefinition MetricsPathExpanded { get; } = Define("generation.experiment.metrics-path-expanded", "Metrics path expanded", "Metrics paths were expanded for a metrics-driven approach.");
    public static RuleDefinition MetricsPathSkippedForNaive { get; } = Define("generation.experiment.metrics-path-skipped-naive", "Metrics path skipped for naive approach", "Metrics path expansion was skipped for the naive approach.");
    public static RuleDefinition ProviderSkippedUnusable { get; } = Define("generation.experiment.provider-skipped-unusable", "Provider skipped as unusable", "A configured provider was skipped because its configuration was unusable.");
    public static RuleDefinition ResumeCompletedItemSkipped { get; } = Define("generation.experiment.resume-completed-item-skipped", "Completed matrix item skipped", "A matrix item was skipped because a completed result already exists.");
    public static RuleDefinition ResumeStaleRunningReset { get; } = Define("generation.experiment.resume-stale-running-reset", "Stale running item reset", "A stale running matrix item was reset for retry.");
    public static RuleDefinition ResumeRunningPreserved { get; } = Define("generation.experiment.resume-running-preserved", "Running item preserved", "A running matrix item was preserved because it has not exceeded the timeout.");
    public static RuleDefinition ResumeResultsFileRewritten { get; } = Define("generation.experiment.resume-results-file-rewritten", "Results file rewritten on resume", "The results file was rewritten on resume.");
    public static RuleDefinition ResumeResultsFileAppended { get; } = Define("generation.experiment.resume-results-file-appended", "Results file appended on resume", "The results file was appended on resume.");
    public static RuleDefinition ResumeAttemptKeyGenerated { get; } = Define("generation.experiment.resume-attempt-key-generated", "Deterministic attempt key generated", "A deterministic resume key was generated from matrix values.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        HistoryComparisonExpanded,
        SingleHistoryModeUsed,
        StepAblationDisabled,
        StepAblationBaselineIncluded,
        StepAblationAllDisabledExcluded,
        StepAblationCapped,
        StepAblationVariantCount,
        MetricsPathExpanded,
        MetricsPathSkippedForNaive,
        ProviderSkippedUnusable,
        ResumeCompletedItemSkipped,
        ResumeStaleRunningReset,
        ResumeRunningPreserved,
        ResumeResultsFileRewritten,
        ResumeResultsFileAppended,
        ResumeAttemptKeyGenerated
    ];

    private static RuleDefinition Define(string id, string name, string description)
    {
        return new RuleDefinition
        {
            Id = id,
            Version = Version,
            Name = name,
            Description = description,
            Category = Category
        };
    }
}
