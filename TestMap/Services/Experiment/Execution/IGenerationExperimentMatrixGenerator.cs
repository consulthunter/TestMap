using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Services.Experiment.Execution;

public interface IGenerationExperimentMatrixGenerator
{
    /// <summary>
    /// Expands the matrix over explicit generation-lane arms. Several arms may share a provider
    /// and differ only by model.
    /// </summary>
    GenerationExperimentMatrix Generate(
        ExperimentConfig config,
        IReadOnlyList<GenerationLlmArm> arms);

    /// <summary>
    /// Expands the matrix over providers, using each provider's configured model. Equivalent to
    /// passing one <see cref="GenerationLlmArm.ForProvider"/> per provider.
    /// </summary>
    GenerationExperimentMatrix Generate(
        ExperimentConfig config,
        IReadOnlyList<AiProvider> providers);
}
