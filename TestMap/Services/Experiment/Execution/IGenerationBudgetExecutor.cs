namespace TestMap.Services.Experiment.Execution;

public interface IGenerationBudgetExecutor
{
    Task<IReadOnlyList<GeneratedCandidateEvaluation>> ExecuteAsync(
        GenerationBudgetExecutionRequest request,
        CancellationToken cancellationToken = default);
}
