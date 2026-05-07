using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Rules;

namespace TestMap.Services.Experiment.Execution;

public sealed class StepAblationVariantGenerationResult
{
    public IReadOnlyList<GenerationStepConfig> Variants { get; init; } = [];
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
