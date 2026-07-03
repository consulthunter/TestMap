using TestMap.Models.Rules;

namespace TestMap.Services.TestGeneration.Validation;

public sealed class RoslynPreBuildDecision
{
    public bool ShouldBuild { get; init; }
    public GenerationValidationConfidence Confidence { get; init; } = GenerationValidationConfidence.Low;
    public GenerationValidationFailureClass? FailureClass { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
