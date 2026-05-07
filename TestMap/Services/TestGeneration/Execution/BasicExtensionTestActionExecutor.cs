using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.Editing;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Execution;

public sealed class BasicExtensionTestActionExecutor : ITestActionExecutor
{
    private readonly ITestCodeEditService _editor;

    public BasicExtensionTestActionExecutor(ITestCodeEditService editor)
    {
        _editor = editor;
    }

    public TestActionExecutorMode Mode => TestActionExecutorMode.BasicExtension;

    public Task<TestActionExecutionResult> ExecuteAsync(
        CandidateMethodContext context,
        string generatedTest,
        string? generatedTestMethodName,
        CancellationToken cancellationToken = default)
    {
        var appendResult = _editor.AppendTestMethodWithResult(context, generatedTest);
        return Task.FromResult(new TestActionExecutionResult
        {
            Success = appendResult.Success,
            AppliedFilePath = context.TestFilePath,
            AppliedTestMethodName = generatedTestMethodName,
            ActionKind = CandidateActionKind.ExtendExistingTestSuite,
            ErrorMessage = appendResult.Success
                ? null
                : appendResult.ErrorMessage ?? $"Failed to append generated test to {context.TestFilePath}.",
            RuleDecisions = appendResult.RuleDecisions
        });
    }
}
