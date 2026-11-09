using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Models.Configuration.Experiment;

public sealed class ExperimentToolConfig
{
    /// <summary>
    /// Unique identifier for this entry. Names the attempt directory and the persisted
    /// ToolAttempt row, and is what ExperimentConfig.Evaluation.ToolIds selects on. Several
    /// entries may run the same tool against different providers (copilot-openai,
    /// copilot-custom), so this is an instance name, not the tool family — see
    /// <see cref="ImageKey"/> and <see cref="Family"/>.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Key into RuntimeConfig.Docker.Images.AgentTools. Defaults to Id when null.
    /// </summary>
    public string? ImageKey { get; init; }

    /// <summary>
    /// Tool family: the shared image, runner script and provider plumbing behind this entry.
    /// Everything that asks "which tool is this?" — secret forwarding, provider env vars, log
    /// and telemetry parsing — must branch on this rather than <see cref="Id"/>, or
    /// provider-suffixed entries silently lose their configuration.
    /// </summary>
    public string Family => string.IsNullOrWhiteSpace(ImageKey) ? Id : ImageKey;

    /// <summary>
    /// Optional provider override. When null, inherits the effective generation provider.
    /// </summary>
    public AiProvider? Provider { get; init; }

    /// <summary>
    /// Optional model override. When null, inherits the effective generation model.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Optional endpoint override for the tool lane. This is useful when the host lane
    /// reaches a service through localhost but Docker tools need host.docker.internal.
    /// </summary>
    public string? Endpoint { get; init; }

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
