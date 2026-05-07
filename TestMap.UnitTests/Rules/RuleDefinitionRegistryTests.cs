using TestMap.Rules;
using TestMap.Rules.Generation;
using TestMap.Rules.ProjectDiscovery;
using TestMap.Rules.TestExecution;
using TestMap.Rules.TestMetadata;

namespace TestMap.UnitTests.Rules;

public sealed class RuleDefinitionRegistryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void All_IncludesEveryDomainRuleDefinition()
    {
        var registryIds = RuleDefinitionRegistry.All
            .Select(x => (x.Id, x.Version))
            .ToHashSet();

        Assert.True(ProjectDiscoveryRuleDefinitions.All.All(x => registryIds.Contains((x.Id, x.Version))));
        Assert.True(TestExecutionRuleDefinitions.All.All(x => registryIds.Contains((x.Id, x.Version))));
        Assert.True(TestMetadataRuleDefinitions.All.All(x => registryIds.Contains((x.Id, x.Version))));
        Assert.True(GenerationEvidenceRuleDefinitions.All.All(x => registryIds.Contains((x.Id, x.Version))));
        Assert.True(GenerationValidationRuleDefinitions.All.All(x => registryIds.Contains((x.Id, x.Version))));
        Assert.True(GenerationAcceptanceRuleDefinitions.All.All(x => registryIds.Contains((x.Id, x.Version))));
        Assert.True(GenerationClassificationRuleDefinitions.All.All(x => registryIds.Contains((x.Id, x.Version))));
        Assert.True(GenerationExperimentRuleDefinitions.All.All(x => registryIds.Contains((x.Id, x.Version))));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void All_DoesNotContainDuplicateRuleVersions()
    {
        var duplicates = RuleDefinitionRegistry.All
            .GroupBy(x => (x.Id, x.Version))
            .Where(x => x.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }
}
