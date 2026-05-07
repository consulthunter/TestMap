using TestMap.Models.Rules;

namespace TestMap.Rules.Generation;

public static class GenerationEvidenceRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "GenerationEvidence";

    public static RuleDefinition ProjectContextIncluded { get; } = Define(
        "generation.evidence.project-context-included",
        "Project context included",
        "Basic method, source, test framework, and existing test context are included.");

    public static RuleDefinition NaiveSuppressesMetricEvidence { get; } = Define(
        "generation.evidence.naive-suppresses-metrics",
        "Naive generation suppresses metric evidence",
        "Naive generation uses the same target candidate but does not expose explicit coverage or mutation evidence.");

    public static RuleDefinition CoverageEvidenceIncluded { get; } = Define(
        "generation.evidence.coverage-included",
        "Coverage evidence included",
        "Coverage-driven generation includes coverage gap evidence.");

    public static RuleDefinition CoverageEvidenceUnavailable { get; } = Define(
        "generation.evidence.coverage-unavailable",
        "Coverage evidence unavailable",
        "Coverage-driven generation requested coverage evidence, but no coverage gaps were available.");

    public static RuleDefinition MutationEvidenceIncluded { get; } = Define(
        "generation.evidence.mutation-included",
        "Mutation evidence included",
        "Mutation-driven generation includes surviving or no-coverage mutant evidence.");

    public static RuleDefinition MutationEvidenceUnavailable { get; } = Define(
        "generation.evidence.mutation-unavailable",
        "Mutation evidence unavailable",
        "Mutation-driven generation requested mutation evidence, but no surviving or no-coverage mutants were available.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        ProjectContextIncluded,
        NaiveSuppressesMetricEvidence,
        CoverageEvidenceIncluded,
        CoverageEvidenceUnavailable,
        MutationEvidenceIncluded,
        MutationEvidenceUnavailable
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
