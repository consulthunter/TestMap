using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Execution;

public interface ITestActionExecutor
{
    TestActionExecutorMode Mode { get; }

    Task<TestActionExecutionResult> ExecuteAsync(
        CandidateMethodContext context,
        string generatedTest,
        string? generatedTestMethodName,
        CancellationToken cancellationToken = default);
}

public sealed class TestActionExecutionResult
{
    public bool Success { get; init; }
    public string? AppliedFilePath { get; init; }
    public string? AppliedTestMethodName { get; init; }
    public CandidateActionKind ActionKind { get; init; }
    public string? ErrorMessage { get; init; }
}