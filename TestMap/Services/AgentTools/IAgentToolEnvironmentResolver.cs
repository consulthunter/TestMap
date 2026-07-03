using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Services.AgentTools;

public sealed class ToolEnvironmentResolution
{
    /// <summary>Normalized TESTMAP_LLM_* env vars for the container.</summary>
    public IReadOnlyDictionary<string, string> NormalizedVars { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Tool-specific env vars for the container (non-secret).</summary>
    public IReadOnlyDictionary<string, string> ToolVars { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Non-secret metadata to persist (provider id, model, base url).</summary>
    public IReadOnlyDictionary<string, string> PersistableMetadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Names of required env vars that are missing on the host. No values.</summary>
    public IReadOnlyList<string> MissingRequiredSecrets { get; init; } = [];

    /// <summary>Compatibility warnings (e.g., claude tool with a non-Anthropic provider).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public bool IsValid => MissingRequiredSecrets.Count == 0;
}

public interface IAgentToolEnvironmentResolver
{
    ToolEnvironmentResolution Resolve(
        ExperimentToolConfig tool,
        AiProviderConfig providers,
        GenerationConfig effectiveGenerationConfig);
}
