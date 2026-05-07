using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Services.Experiment.Execution;

public interface IGenerationExperimentMatrixGenerator
{
    GenerationExperimentMatrix Generate(
        ExperimentConfig config,
        IReadOnlyList<AiProvider> providers);
}
