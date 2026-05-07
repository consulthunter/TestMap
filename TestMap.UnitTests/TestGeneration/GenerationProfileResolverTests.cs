using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GenerationProfileResolverTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveBaseProfile_InheritsRegularGenerationDefaults()
    {
        var generationConfig = new GenerationConfig
        {
            Objective = TestGenerationObjective.TestSuiteExpansion,
            Provider = AiProvider.OpenAi,
            Strategy = TestGenerationApproach.MetricsDriven,
            MetricsPath = MetricsDrivenPath.Mutation,
            ContextMode = GenerationContextMode.NoHistory,
            Temperature = 0.3,
            StepErrorRetries = 2,
            StepRetryDelayMs = 250
        };
        generationConfig.Steps.EnableContextGraph = true;

        var profile = GenerationProfileResolver.ResolveBaseProfile(generationConfig, "gpt-profile");

        Assert.Equal(TestGenerationObjective.TestSuiteExpansion, profile.Objective);
        Assert.Equal(AiProvider.OpenAi, profile.Provider);
        Assert.Equal("gpt-profile", profile.ModelName);
        Assert.Equal(TestGenerationApproach.MetricsDriven, profile.Approach);
        Assert.Equal(MetricsDrivenPath.Mutation, profile.MetricsPath);
        Assert.Equal(TestActionExecutorMode.BasicExtension, profile.Executor);
        Assert.Equal(GenerationBudgetMode.PassAt1RepairAt5, profile.BudgetMode);
        Assert.Equal(GenerationContextMode.NoHistory, profile.ContextMode);
        Assert.True(profile.Steps.EnableContextGraph);
        Assert.Equal(0.3, profile.Temperature);
        Assert.Equal(2, profile.StepErrorRetries);
        Assert.Equal(250, profile.StepRetryDelayMs);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveEffectiveProfile_AppliesExperimentMatrixOverrides()
    {
        var generationConfig = new GenerationConfig
        {
            Provider = AiProvider.OpenAi,
            Strategy = TestGenerationApproach.MetricsDriven,
            MetricsPath = MetricsDrivenPath.Coverage,
            ContextMode = GenerationContextMode.ChainedHistory,
            Temperature = 0.1,
            StepErrorRetries = 1,
            StepRetryDelayMs = 100
        };
        var experimentConfig = new ExperimentConfig
        {
            Objective = TestGenerationObjective.TestSuiteExpansion,
            StepErrorRetries = 3,
            StepRetryDelayMs = 750
        };
        var steps = new GenerationStepConfig
        {
            VariantId = "ablated-Scenario",
            EnableScenario = false
        };
        var matrixItem = new GenerationExperimentMatrixItem
        {
            VariantId = "variant",
            Provider = AiProvider.GoogleGemini,
            ModelName = "gemini-profile",
            Approach = TestGenerationApproach.Naive,
            MetricsPath = null,
            ContextMode = GenerationContextMode.NoHistory,
            BudgetMode = GenerationBudgetMode.PassAt5,
            Steps = steps,
            Temperature = 0.8
        };

        var profile = GenerationProfileResolver.ResolveEffectiveProfile(
            generationConfig,
            experimentConfig,
            matrixItem);

        Assert.Equal(AiProvider.GoogleGemini, profile.Provider);
        Assert.Equal("gemini-profile", profile.ModelName);
        Assert.Equal(TestGenerationApproach.Naive, profile.Approach);
        Assert.Null(profile.MetricsPath);
        Assert.Equal(GenerationBudgetMode.PassAt5, profile.BudgetMode);
        Assert.Equal(GenerationContextMode.NoHistory, profile.ContextMode);
        Assert.Equal("ablated-Scenario", profile.Steps.VariantId);
        Assert.False(profile.Steps.EnableScenario);
        Assert.Equal(0.8, profile.Temperature);
        Assert.Equal(3, profile.StepErrorRetries);
        Assert.Equal(750, profile.StepRetryDelayMs);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveEffectiveProfile_ClonesStepVariantSoBaselineIsNotMutated()
    {
        var generationConfig = new GenerationConfig();
        var experimentConfig = new ExperimentConfig();
        var steps = new GenerationStepConfig
        {
            VariantId = "baseline",
            EnableContextGraph = true
        };
        var matrixItem = new GenerationExperimentMatrixItem
        {
            VariantId = "variant",
            Provider = AiProvider.OpenAi,
            ModelName = "gpt-profile",
            Approach = TestGenerationApproach.MetricsDriven,
            MetricsPath = MetricsDrivenPath.CoverageAndMutation,
            ContextMode = GenerationContextMode.ChainedHistory,
            BudgetMode = GenerationBudgetMode.PassAt1,
            Steps = steps,
            Temperature = 0.0
        };

        var profile = GenerationProfileResolver.ResolveEffectiveProfile(
            generationConfig,
            experimentConfig,
            matrixItem);

        profile.Steps.EnableContextGraph = false;

        Assert.True(steps.EnableContextGraph);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StableHash_ChangesWhenProfileChanges()
    {
        var profile = GenerationProfileResolver.ResolveBaseProfile(new GenerationConfig(), "gpt-profile");
        var originalHash = profile.ToStableHash();

        profile.Temperature = 0.2;

        Assert.NotEqual(originalHash, profile.ToStableHash());
        Assert.NotNull(JsonDocument.Parse(profile.ToStableJson()));
    }
}
