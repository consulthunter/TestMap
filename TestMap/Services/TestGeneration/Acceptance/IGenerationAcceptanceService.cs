using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.Services.TestGeneration.Acceptance;

public interface IGenerationAcceptanceService
{
    GenerationAcceptanceResult Evaluate(
        GenerationValidationResult validation,
        TestAcceptanceConfig config);
}
