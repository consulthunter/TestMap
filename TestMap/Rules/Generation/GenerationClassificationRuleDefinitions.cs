using TestMap.Models.Rules;

namespace TestMap.Rules.Generation;

public static class GenerationClassificationRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "GenerationClassification";

    public static RuleDefinition ValidatedEvidencePositive { get; } = Define("generation.classification.validated-evidence-positive", "Validated evidence-positive generated test", "The generated test validated and improved a configured evidence signal.");
    public static RuleDefinition FailedEvidencePositive { get; } = Define("generation.classification.failed-evidence-positive", "Failed evidence-positive generated test", "The generated test failed validation but improved a configured evidence signal.");
    public static RuleDefinition ValidatedLowImpact { get; } = Define("generation.classification.validated-low-impact", "Validated low-impact generated test", "The generated test validated without improving the configured evidence signal.");
    public static RuleDefinition ValidationFailedCompilation { get; } = Define("generation.classification.validation-failed-compilation", "Validation failed during compilation", "The generated test failed validation because compilation failed.");
    public static RuleDefinition ValidationFailedExecution { get; } = Define("generation.classification.validation-failed-execution", "Validation failed before execution", "The generated test failed validation before useful evidence was observed.");
    public static RuleDefinition ValidationFailedAssertion { get; } = Define("generation.classification.validation-failed-assertion", "Validation failed during assertion", "The generated test failed assertions without useful evidence.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        ValidatedEvidencePositive,
        FailedEvidencePositive,
        ValidatedLowImpact,
        ValidationFailedCompilation,
        ValidationFailedExecution,
        ValidationFailedAssertion
    ];

    private static RuleDefinition Define(string id, string name, string description)
    {
        return new RuleDefinition
        {
            Id = id,
            Version = Version,
            Name = name,
            Description = description,
            Category = Category
        };
    }
}
