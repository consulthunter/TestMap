using TestMap.Models.Experiment;
using TestMap.Models.Rules;

namespace TestMap.Services.TestGeneration.Classification;

public sealed class GenerationClassificationResult
{
    public GeneratedTestClassification Classification { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
