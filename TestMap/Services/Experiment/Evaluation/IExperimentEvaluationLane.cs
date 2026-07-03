using TestMap.Models.Experiment;
using TestMap.Models.AgentTools;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.Experiment.Evaluation;

public sealed class ExperimentEvaluationPlanningContext
{
    public ExperimentRun ExperimentRun { get; init; } = new();
    public IReadOnlyList<CandidateMethod> Candidates { get; init; } = [];
}

public sealed class ExperimentEvaluationWorkItemContext
{
    public ExperimentMatrixWorkItem WorkItem { get; init; } = new();
    public CandidateMethod Candidate { get; init; } = new();
    public int? TargetedBaselineId { get; init; }

    /// <summary>
    /// Full method context including source location, test framework, example tests etc.
    /// Null in Phase 1 stubs; required in Phase 2 when task card building is wired up.
    /// </summary>
    public CandidateMethodContext? MethodContext { get; init; }
}

public sealed class ExperimentEvaluationAttemptResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public ToolAttempt? ToolAttempt { get; init; }

    /// <summary>
    /// Workspace-relative paths of files changed by the tool, as read from
    /// <c>changed-files.txt</c> in the attempt artifact directory. Empty when no files
    /// changed or when collection was not performed.
    /// </summary>
    public IReadOnlyList<string> ChangedFiles { get; init; } = [];
}

public interface IExperimentEvaluationLane
{
    string LaneId { get; }

    Task<IReadOnlyList<ExperimentMatrixWorkItem>> CreateWorkItemsAsync(
        ExperimentEvaluationPlanningContext context,
        CancellationToken cancellationToken);

    Task<ExperimentEvaluationAttemptResult> ExecuteAsync(
        ExperimentEvaluationWorkItemContext context,
        CancellationToken cancellationToken);
}
