using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.Experiment.Execution;

public sealed class TargetedBaselineResult
{
    public int? TestRunId { get; init; }
    public bool Ran { get; init; }

    /// <summary>The run compiled, executed tests without a failure analysis, and was persisted.</summary>
    public bool Usable { get; init; }

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
