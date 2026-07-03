using TestMap.Models.AgentTools;
using TestMap.Models.Configuration.Experiment;

namespace TestMap.Services.AgentTools;

public sealed class ToolAvailabilityResult
{
    public string ToolId { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public string? ImageName { get; init; }
    public string? DetectedVersion { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<string> MissingSecrets { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class ToolRunRequest
{
    public ToolAttempt Attempt { get; init; } = new();
    public ExperimentToolConfig ToolConfig { get; init; } = new();
    public string WorkspacePath { get; init; } = string.Empty;
    public string ArtifactPath { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> ResolvedEnvironment { get; init; } =
        new Dictionary<string, string>();
}

public sealed class ToolRunPreparationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ToolRunResult
{
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = string.Empty;
    public string StdErr { get; init; } = string.Empty;
    public TimeSpan Elapsed { get; init; }
    public bool TimedOut { get; init; }
    public string? ToolVersion { get; init; }
}

public sealed class ToolRunCollectionResult
{
    public string PatchDiff { get; init; } = string.Empty;
    public IReadOnlyList<string> ChangedFiles { get; init; } = [];
    public string GitStatusBefore { get; init; } = string.Empty;
    public string GitStatusAfter { get; init; } = string.Empty;
}

public interface IAgentToolRunner
{
    Task<ToolAvailabilityResult> CheckAvailabilityAsync(
        ExperimentToolConfig tool,
        CancellationToken cancellationToken);

    Task<ToolRunPreparationResult> PrepareAsync(
        ToolRunRequest request,
        CancellationToken cancellationToken);

    Task<ToolRunResult> RunAsync(
        ToolRunRequest request,
        CancellationToken cancellationToken);

    Task<ToolRunCollectionResult> CollectAsync(
        ToolRunRequest request,
        CancellationToken cancellationToken);
}
