using TestMap.Services.TestGeneration.Evidence;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Validation;

public interface IGenerationValidationService
{
    GenerationValidationResult Validate(
        GeneratedTestExecutionResult execution,
        CandidateMethodContext context,
        GenerationEvidencePackage evidence);
}
