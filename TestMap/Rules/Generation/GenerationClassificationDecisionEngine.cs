using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.Rules.Generation;

public static class GenerationClassificationDecisionEngine
{
    public static (GeneratedTestClassification Classification, RuleDecisionRecord Decision) Classify(
        GenerationValidationResult validation)
    {
        if (!validation.CompilationSucceeded)
            return Create(GeneratedTestClassification.ValidationFailed, GenerationClassificationRuleDefinitions.ValidationFailedCompilation);

        if (validation.TestsExecuted && validation.AllTestsPassed && validation.HasUsefulMetricSignal)
            return Create(GeneratedTestClassification.ValidatedEvidencePositive, GenerationClassificationRuleDefinitions.ValidatedEvidencePositive);

        if (validation.TestsExecuted && !validation.AllTestsPassed && validation.HasUsefulMetricSignal)
            return Create(GeneratedTestClassification.FailedEvidencePositive, GenerationClassificationRuleDefinitions.FailedEvidencePositive);

        if (validation.TestsExecuted && validation.AllTestsPassed)
            return Create(GeneratedTestClassification.ValidatedLowImpact, GenerationClassificationRuleDefinitions.ValidatedLowImpact);

        if (validation.TestsExecuted)
            return Create(GeneratedTestClassification.ValidationFailed, GenerationClassificationRuleDefinitions.ValidationFailedAssertion);

        return Create(GeneratedTestClassification.ValidationFailed, GenerationClassificationRuleDefinitions.ValidationFailedExecution);
    }

    private static (GeneratedTestClassification Classification, RuleDecisionRecord Decision) Create(
        GeneratedTestClassification classification,
        RuleDefinition rule)
    {
        return (
            classification,
            RuleDecisionFactory.CreateDecision(
                "GenerationClassification",
                classification.ToString(),
                rule,
                RuleConfidence.High,
                notes: rule.Description));
    }
}
