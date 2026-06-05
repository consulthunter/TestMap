using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.Experiment.Execution;

public sealed class TargetedBaselineResult
{
    public int? TestRunId { get; init; }
    public bool Ran { get; init; }
    public string SkipReason { get; init; } = string.Empty;
}

public interface ITargetedBaselineService
{
    Task<TargetedBaselineResult> RunAsync(
        int experimentRunId,
        CandidateMethod candidate,
        CandidateMethodContext methodContext,
        CancellationToken cancellationToken = default);
}
