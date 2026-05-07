using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment.Execution;

public sealed class GeneratedCandidateEvaluation
{
    public required GenerationAttempt Attempt { get; init; }
    public int AttemptNumber { get; init; }
    public int? ParentAttemptNumber { get; init; }
    public bool IsRepairAttempt { get; init; }
}
