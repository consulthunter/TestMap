using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment;

public interface IExperimentOrchestrationService
{
    Task<ExperimentRun> RunExperimentAsync(
        ExperimentConfiguration config,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GenerationAttempt>> ExecuteGenerationAttemptAsync(
        CandidateMethod candidateMethod,
        CandidateMethodContext context,
        AiProvider provider,
        GenerationStrategy strategy,
        CancellationToken cancellationToken = default);
}
