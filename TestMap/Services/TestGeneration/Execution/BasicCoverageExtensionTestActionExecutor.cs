using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.Editing;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Execution;

public sealed class BasicCoverageExtensionTestActionExecutor : ITestActionExecutor
{
    private readonly ITestCodeEditService _editor;

    public BasicCoverageExtensionTestActionExecutor(ITestCodeEditService editor)
    {
        _editor = editor;
    }

    public TestActionExecutorMode Mode => TestActionExecutorMode.BasicCoverageExtension;

    public Task<TestActionExecutionResult> ExecuteAsync(
        CandidateMethodContext context,
        string generatedTest,
        string? generatedTestMethodName,
        CancellationToken cancellationToken = default)
    {
        var success = _editor.AppendTestMethod(context, generatedTest);
        return Task.FromResult(new TestActionExecutionResult
        {
            Success = success,
            AppliedFilePath = context.TestFilePath,
            AppliedTestMethodName = generatedTestMethodName,
            ActionKind = CandidateActionKind.ExtendExistingTestSuite,
            ErrorMessage = success ? null : $"Failed to append generated test to {context.TestFilePath}."
        });
    }
}