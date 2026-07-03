using TestMap.Models.Experiment;
using TestMap.Models.Rules;

namespace TestMap.Services.Experiment.Execution;

public sealed class ExperimentResumeDecision
{
    public required ExperimentMatrixWorkItem WorkItem { get; init; }
    public bool ShouldExecute { get; init; }
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
