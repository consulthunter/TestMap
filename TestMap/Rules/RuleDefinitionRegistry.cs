using TestMap.Models.Rules;
using TestMap.Rules.Generation;
using TestMap.Rules.ProjectDiscovery;
using TestMap.Rules.TestExecution;
using TestMap.Rules.TestMetadata;

namespace TestMap.Rules;

public static class RuleDefinitionRegistry
{
    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        ..ProjectDiscoveryRuleDefinitions.All,
        ..TestExecutionRuleDefinitions.All,
        ..TestMetadataRuleDefinitions.All,
        ..GenerationEvidenceRuleDefinitions.All,
        ..GenerationValidationRuleDefinitions.All,
        ..GenerationAcceptanceRuleDefinitions.All,
        ..GenerationClassificationRuleDefinitions.All,
        ..GenerationAppendRuleDefinitions.All,
        ..GenerationExperimentRuleDefinitions.All
    ];
}
