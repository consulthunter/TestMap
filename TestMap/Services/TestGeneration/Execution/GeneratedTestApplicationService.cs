using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Execution;

public sealed class GeneratedTestApplicationService : IGeneratedTestApplicationService
{
    private readonly IReadOnlyDictionary<TestActionExecutorMode, ITestActionExecutor> _executors;

    public GeneratedTestApplicationService(IEnumerable<ITestActionExecutor> executors)
    {
        _executors = executors.ToDictionary(x => x.Mode);
    }

    public async Task<GeneratedTestApplicationResult> ApplyAsync(
        CandidateMethodContext context,
        string generatedTest,
        string testMethodName,
        TestActionExecutorMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!_executors.TryGetValue(mode, out var executor))
            throw new InvalidOperationException($"No test action executor is registered for '{mode}'.");

        var result = await executor.ExecuteAsync(
            context,
            generatedTest,
            testMethodName,
            cancellationToken);

        return new GeneratedTestApplicationResult
        {
            Success = result.Success,
            AppliedFilePath = result.AppliedFilePath,
            AppliedTestMethodName = result.AppliedTestMethodName,
            ActionKind = result.ActionKind,
            ErrorMessage = result.ErrorMessage,
            RuleDecisions = result.RuleDecisions
        };
    }
}
