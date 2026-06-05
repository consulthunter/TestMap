using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Models.Configuration.Experiment;

public sealed class ExperimentToolConfig
{
    /// <summary>
    /// Unique identifier. Must match a key in RuntimeConfig.Docker.Images.AgentTools.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Key into RuntimeConfig.Docker.Images.AgentTools. Defaults to Id when null.
    /// </summary>
    public string? ImageKey { get; init; }

    /// <summary>
    /// Optional provider override. When null, inherits the effective generation provider.
    /// </summary>
    public AiProvider? Provider { get; init; }

    /// <summary>
    /// Optional model override. When null, inherits the effective generation model.
    /// </summary>
    public string? Model { get; init; }

    public int TimeoutMinutes { get; init; } = 45;

    /// <summary>
    /// Additional env vars injected into the container verbatim. Values here are non-secret.
    /// Secrets are resolved via IAgentToolEnvironmentResolver.
    /// </summary>
    public Dictionary<string, string> Environment { get; init; } = new();

    /// <summary>
    /// Names of required host env vars whose absence should fail availability checks.
    /// Values are never logged or persisted.
    /// </summary>
    public IReadOnlyList<string> RequiredEnvironmentVariables { get; init; } = [];

    public Dictionary<string, string> Metadata { get; init; } = new();
}
