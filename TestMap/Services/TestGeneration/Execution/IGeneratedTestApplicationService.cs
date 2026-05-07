using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Execution;

public interface IGeneratedTestApplicationService
{
    Task<GeneratedTestApplicationResult> ApplyAsync(
        CandidateMethodContext context,
        string generatedTest,
        string testMethodName,
        TestActionExecutorMode mode,
        CancellationToken cancellationToken = default);
}

public sealed class GeneratedTestApplicationResult
{
    public bool Success { get; init; }
    public string? AppliedFilePath { get; init; }
    public string? AppliedTestMethodName { get; init; }
    public CandidateActionKind ActionKind { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
