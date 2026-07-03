using System.Text.Json;
using TestMap.Services.Configuration;

namespace TestMap.Services.Experiment.TaskCards;

public sealed class TaskCard
{
    public string Objective { get; init; } = "TestSuiteExpansion";
    public string TargetMemberName { get; init; } = string.Empty;
    public string TargetMemberSignature { get; init; } = string.Empty;
    public string TargetMemberWeakness { get; init; } = string.Empty;
    public string AccessStrategy { get; init; } = string.Empty;
    public IReadOnlyList<string> MappedTests { get; init; } = [];
    public IReadOnlyList<string> Constraints { get; init; } =
    [
        "Add or extend tests only.",
        "Do not remove or modify existing tests.",
        "Do not change production code unless necessary to fix a clear defect.",
        "Avoid unrelated formatting or project-file churn."
    ];
}

public sealed class TaskCardContent
{
    public TaskCard Card { get; init; } = new();
    public string Prompt { get; init; } = string.Empty;
    public string EvidenceSummary { get; init; } = string.Empty;
}

/// <summary>
/// Writes task-card.json, prompt.md, and evidence-summary.md to the {workspacePath}/.testmap/
/// directory before a tool container run.
/// </summary>
public sealed class TaskCardWriter
{
    private readonly JsonSerializerOptions _jsonOptions;

    public TaskCardWriter()
    {
        _jsonOptions = ConfigJsonSerializer.CreateOptions();
        _jsonOptions.WriteIndented = true;
    }

    /// <summary>
    /// Writes task-card.json, prompt.md, and evidence-summary.md to {workspacePath}/.testmap/.
    /// Creates the directory if it does not exist.
    /// </summary>
    public async Task WriteAsync(
        string workspacePath,
        TaskCardContent content,
        CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(workspacePath, ".testmap");
        Directory.CreateDirectory(dir);

        var cardJson = JsonSerializer.Serialize(content.Card, _jsonOptions);
        await File.WriteAllTextAsync(Path.Combine(dir, "task-card.json"), cardJson, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(dir, "prompt.md"), content.Prompt, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(dir, "evidence-summary.md"), content.EvidenceSummary, cancellationToken);
    }
}
