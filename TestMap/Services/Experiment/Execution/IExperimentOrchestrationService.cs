using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.Experiment.Execution;

public interface IExperimentOrchestrationService
{
    Task<ExperimentRun> RunExperimentAsync(
        ExperimentConfig config,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GenerationAttempt>> ExecuteGenerationAttemptAsync(
        CandidateMethod candidateMethod,
        CandidateMethodContext context,
        AiProvider provider,
        GenerationBudgetMode budgetMode,
        CancellationToken cancellationToken = default);
}
