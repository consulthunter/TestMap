using TestMap.Services.TestGeneration.Validation;

namespace TestMap.Services.TestGeneration.Classification;

public interface IGenerationClassificationService
{
    GenerationClassificationResult Classify(GenerationValidationResult validation);
}
