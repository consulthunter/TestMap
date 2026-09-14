using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Rules;

namespace TestMap.Services.Experiment.Execution;

public sealed class GenerationExperimentMatrix
{
    public IReadOnlyList<GenerationExperimentMatrixItem> Items { get; init; } = [];
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}

/// <summary>
/// A resolved generation-lane arm: the provider, the model it runs, and any endpoint override.
/// Built either from ExperimentConfig.LlmArms or, for configs that do not declare arms, one per
/// included provider using that provider's configured model.
/// </summary>
public sealed class GenerationLlmArm
{
    public required string Id { get; init; }
    public required AiProvider Provider { get; init; }
    public string ModelName { get; init; } = string.Empty;
    public string? Endpoint { get; init; }

    /// <summary>
    /// The arm a config without an explicit LlmArms list produces for a provider: id is the
    /// provider name, so variant ids stay byte-identical to what that config produced before arms
    /// existed.
    /// </summary>
    public static GenerationLlmArm ForProvider(AiProvider provider, string modelName) =>
        new()
        {
            Id = provider.ToString(),
            Provider = provider,
            ModelName = modelName
        };
}

public sealed class GenerationExperimentMatrixItem
{
    public required string VariantId { get; init; }

    /// <summary>
    /// Identifies the arm this item came from. Equals the provider name for configs that do not
    /// declare LlmArms; distinguishes arms that share a provider when they do.
    /// </summary>
    public string ArmId { get; init; } = string.Empty;

    public required AiProvider Provider { get; init; }
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    /// Endpoint override for this arm, or null to use the provider's configured endpoint.
    /// </summary>
    public string? Endpoint { get; init; }
    public required TestGenerationApproach Approach { get; init; }
    public MetricsDrivenPath? MetricsPath { get; init; }
    public required GenerationContextMode ContextMode { get; init; }
    public required GenerationBudgetMode BudgetMode { get; init; }
    public required GenerationStepConfig Steps { get; init; }
    public required double Temperature { get; init; }
    public GenerationProfile? EffectiveProfile { get; init; }
}
