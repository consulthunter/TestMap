using TestMap.Models.Rules;

namespace TestMap.Services.TestGeneration.Acceptance;

public sealed class GenerationAcceptanceResult
{
    public bool Accepted { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
