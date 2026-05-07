using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Rules;

namespace TestMap.Services.Experiment.Execution;

public sealed class GenerationExperimentMatrix
{
    public IReadOnlyList<GenerationExperimentMatrixItem> Items { get; init; } = [];
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}

public sealed class GenerationExperimentMatrixItem
{
    public required string VariantId { get; init; }
    public required AiProvider Provider { get; init; }
    public string ModelName { get; init; } = string.Empty;
    public required TestGenerationApproach Approach { get; init; }
    public MetricsDrivenPath? MetricsPath { get; init; }
    public required GenerationContextMode ContextMode { get; init; }
    public required GenerationBudgetMode BudgetMode { get; init; }
    public required GenerationStepConfig Steps { get; init; }
    public required double Temperature { get; init; }
    public GenerationProfile? EffectiveProfile { get; init; }
}
