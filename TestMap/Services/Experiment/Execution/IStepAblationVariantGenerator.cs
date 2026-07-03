using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Services.Experiment.Execution;

public interface IStepAblationVariantGenerator
{
    StepAblationVariantGenerationResult Generate(StepAblationConfig config, GenerationStepConfig baselineSteps);
}
