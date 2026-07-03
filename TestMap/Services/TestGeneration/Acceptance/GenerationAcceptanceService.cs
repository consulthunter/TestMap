using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Rules.Generation;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.Services.TestGeneration.Acceptance;

public sealed class GenerationAcceptanceService : IGenerationAcceptanceService
{
    public GenerationAcceptanceResult Evaluate(
        GenerationValidationResult validation,
        TestAcceptanceConfig config)
    {
        var decisions = GenerationAcceptanceDecisionEngine.Evaluate(validation, config);
        var failure = decisions.FirstOrDefault(x => x.Value.StartsWith("Rejected", StringComparison.Ordinal));

        if (failure != null)
        {
            return new GenerationAcceptanceResult
            {
                Accepted = false,
                Reason = failure.Notes,
                RuleDecisions = decisions
            };
        }

        return new GenerationAcceptanceResult
        {
            Accepted = true,
            Reason = "Accepted.",
            RuleDecisions = decisions
        };
    }
}
