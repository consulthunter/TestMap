using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GenerationExperimentMatrixGeneratorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ExpandsApproachMetricsHistoryBudgetAndStepVariants()
    {
        var config = new TestMapConfig();
        config.AiProviderConfig.OpenAi.Model = "gpt-test";
        config.TestingConfig.GenerationConfig.Steps.EnableContextGraph = true;
        config.TestingConfig.GenerationConfig.Steps.EnableContextResolution = true;
        var generator = new GenerationExperimentMatrixGenerator(config, new StepAblationVariantGenerator());

        var matrix = generator.Generate(
            new ExperimentConfig
            {
                Approaches = [TestGenerationApproach.Naive, TestGenerationApproach.MetricsDriven],
                MetricsPaths = [MetricsDrivenPath.Coverage, MetricsDrivenPath.Mutation],
                CompareHistoryModes = true,
                BudgetModes = [GenerationBudgetMode.PassAt1, GenerationBudgetMode.PassAt5],
                StepAblation = new StepAblationConfig
                {
                    Enabled = true,
                    IncludeBaseline = true,
                    IncludeAllDisabled = false,
                    Steps = [GenerationStepType.Scenario],
                    MaxVariants = 16
                },
                Temperature = 0.4
            },
            [AiProvider.OpenAi]);

        Assert.Equal(12, matrix.Items.Count);
        Assert.Contains(matrix.Items, x => x.Approach == TestGenerationApproach.Naive && x.MetricsPath == null);
        Assert.Contains(matrix.Items, x => x.Approach == TestGenerationApproach.MetricsDriven && x.MetricsPath == MetricsDrivenPath.Coverage);
        Assert.Contains(matrix.Items, x => x.ContextMode == GenerationContextMode.NoHistory);
        Assert.Contains(matrix.Items, x => x.BudgetMode == GenerationBudgetMode.PassAt5);
        Assert.All(matrix.Items, x => Assert.Equal(0.4, x.Temperature));
        Assert.All(matrix.Items, x => Assert.Equal("gpt-test", x.ModelName));
        Assert.All(matrix.Items, x => Assert.NotNull(x.EffectiveProfile));
        Assert.All(matrix.Items, x => Assert.Equal(x.Approach, x.EffectiveProfile!.Approach));
        Assert.All(matrix.Items, x => Assert.Equal(x.MetricsPath, x.EffectiveProfile!.MetricsPath));
        Assert.All(matrix.Items, x => Assert.Equal(x.ContextMode, x.EffectiveProfile!.ContextMode));
        Assert.All(matrix.Items, x => Assert.Equal(x.Temperature, x.EffectiveProfile!.Temperature));
        Assert.All(matrix.Items, x => Assert.True(x.Steps.EnableContextGraph));
        Assert.All(matrix.Items, x => Assert.True(x.Steps.EnableContextResolution));
        Assert.Contains(matrix.RuleDecisions, x => x.Value == "HistoryComparisonExpanded");
        Assert.Contains(matrix.RuleDecisions, x => x.Value == "MetricsPathSkippedForNaive");
        Assert.Contains(matrix.RuleDecisions, x => x.Value == "MetricsPathExpanded");
    }
}
