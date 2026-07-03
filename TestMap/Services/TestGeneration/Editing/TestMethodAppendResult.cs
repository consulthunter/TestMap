using TestMap.Models.Rules;

namespace TestMap.Services.TestGeneration.Editing;

public sealed class TestMethodAppendResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
