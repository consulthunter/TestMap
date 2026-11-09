using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Models.Configuration.Experiment;

/// <summary>
/// One arm of the TestMap generation lane: a provider plus the model to run it against. Arms are
/// the LLM-lane counterpart to <see cref="ExperimentToolConfig"/> — several arms may share a
/// provider and differ only by model, which is how one experiment run compares several open
/// models hosted behind the same custom endpoint instead of re-running the experiment per model.
/// </summary>
public sealed class ExperimentLlmArmConfig
{
    /// <summary>
    /// Unique identifier for this arm. Appears in the matrix variant id and the resume stable key,
    /// so two arms that differ only by endpoint still produce distinct work items.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Provider backing this arm. When null, inherits the effective generation provider from
    /// TestingConfig.GenerationConfig.Provider.
    /// </summary>
    public AiProvider? Provider { get; init; }

    /// <summary>
    /// Model override for this arm. When null, falls back to the provider's configured Model.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Endpoint override for this arm, for providers that have one (CustomOpenAi, Ollama, and the
    /// Google providers). Lets two arms point at differently hosted models without editing
    /// AiProviderConfig between runs. When null, the provider's configured endpoint is used.
    /// </summary>
    public string? Endpoint { get; init; }
}
