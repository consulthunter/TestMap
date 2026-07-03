using TestMap.Rules.Generation;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.Services.TestGeneration.Classification;

public sealed class GenerationClassificationService : IGenerationClassificationService
{
    public GenerationClassificationResult Classify(GenerationValidationResult validation)
    {
        var decision = GenerationClassificationDecisionEngine.Classify(validation);

        return new GenerationClassificationResult
        {
            Classification = decision.Classification,
            Reason = decision.Decision.Notes,
            RuleDecisions = [decision.Decision]
        };
    }
}
