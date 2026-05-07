using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class StepAblationVariantGeneratorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_Disabled_ReturnsBaselineVariant()
    {
        var generator = new StepAblationVariantGenerator();

        var result = generator.Generate(
            new StepAblationConfig { Enabled = false },
            new GenerationStepConfig
            {
                EnableContextGraph = true,
                EnableContextResolution = true
            });

        var variant = Assert.Single(result.Variants);
        Assert.Equal("baseline", variant.VariantId);
        Assert.True(variant.EnableScenario);
        Assert.True(variant.EnableContextGraph);
        Assert.True(variant.EnableContextResolution);
        Assert.Contains(result.RuleDecisions, x => x.Value == "StepAblationDisabled");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ProducesCombinationsAndExcludesAllDisabledWhenConfigured()
    {
        var generator = new StepAblationVariantGenerator();

        var result = generator.Generate(
            new StepAblationConfig
            {
                Enabled = true,
                IncludeBaseline = true,
                IncludeAllDisabled = false,
                Steps = [GenerationStepType.Scenario, GenerationStepType.MethodName],
                MaxVariants = 32
            },
            new GenerationStepConfig
            {
                EnableContextGraph = true,
                EnableContextResolution = true
            });

        Assert.Equal(3, result.Variants.Count);
        Assert.Contains(result.Variants, x => x.VariantId == "baseline" &&
                                             x.EnableContextGraph &&
                                             x.EnableContextResolution);
        Assert.Contains(result.Variants, x => x.VariantId == "ablated-Scenario" && !x.EnableScenario);
        Assert.Contains(result.Variants, x => x.VariantId == "ablated-MethodName" && !x.EnableMethodName);
        Assert.DoesNotContain(result.Variants, x => x.VariantId == "ablated-Scenario-MethodName");
        Assert.Contains(result.RuleDecisions, x => x.Value == "StepAblationAllDisabledExcluded");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_EnforcesMaxVariants()
    {
        var generator = new StepAblationVariantGenerator();

        var result = generator.Generate(
            new StepAblationConfig
            {
                Enabled = true,
                IncludeBaseline = true,
                IncludeAllDisabled = true,
                Steps = [GenerationStepType.Scenario, GenerationStepType.MethodName, GenerationStepType.ArrangePlan],
                MaxVariants = 2
            },
            new GenerationStepConfig());

        Assert.Equal(2, result.Variants.Count);
        Assert.Contains(result.RuleDecisions, x => x.Value == "StepAblationCapped");
    }
}
