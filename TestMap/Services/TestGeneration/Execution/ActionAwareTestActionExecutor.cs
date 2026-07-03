using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.Editing;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Execution;

public sealed class ActionAwareTestActionExecutor : ITestActionExecutor
{
    private readonly ITestCodeEditService _editor;

    public ActionAwareTestActionExecutor(ITestCodeEditService editor)
    {
        _editor = editor;
    }

    public TestActionExecutorMode Mode => TestActionExecutorMode.ActionAware;

    public Task<TestActionExecutionResult> ExecuteAsync(
        CandidateMethodContext context,
        string generatedTest,
        string? generatedTestMethodName,
        CancellationToken cancellationToken = default)
    {
        var action = context.Method.RecommendedAction;
        var success = action switch
        {
            CandidateActionKind.GenerateNewTest => ExecuteGenerateNewTest(context, generatedTest),
            CandidateActionKind.ImproveExistingTest => ExecuteImproveExistingTest(context, generatedTest),
            CandidateActionKind.ExtendExistingTestSuite => ExecuteExtendExistingTest(context, generatedTest),
            CandidateActionKind.Skip => false,
            _ => ExecuteExtendExistingTest(context, generatedTest)
        };

        return Task.FromResult(new TestActionExecutionResult
        {
            Success = success,
            AppliedFilePath = context.TestFilePath,
            AppliedTestMethodName = generatedTestMethodName,
            ActionKind = action,
            ErrorMessage = success ? null : BuildError(action, context)
        });
    }

    private bool ExecuteGenerateNewTest(CandidateMethodContext context, string generatedTest)
    {
        _editor.EnsureTestClassExists(context);
        return _editor.AppendTestMethod(context, generatedTest);
    }

    private bool ExecuteImproveExistingTest(CandidateMethodContext context, string generatedTest)
    {
        if (!string.IsNullOrWhiteSpace(context.Method.ExistingTestMethodName))
            return _editor.ReplaceTestMethod(context, context.Method.ExistingTestMethodName, generatedTest);

        return _editor.AppendTestMethod(context, generatedTest);
    }

    private bool ExecuteExtendExistingTest(CandidateMethodContext context, string generatedTest)
    {
        return _editor.AppendTestMethod(context, generatedTest);
    }

    private static string BuildError(CandidateActionKind action, CandidateMethodContext context)
    {
        return action switch
        {
            CandidateActionKind.Skip =>
                $"Generation for {context.Method.MethodName} was skipped by action-aware execution.",
            _ => $"Failed to apply generated test for {context.Method.MethodName} using action '{action}'."
        };
    }
}