using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.UnitTests.Configuration;

public sealed class ConfigurationDefaultsTests
{
    /// <summary>
    /// Verifies that the root configuration creates all major nested configuration objects by default.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void TestMapConfig_DefaultConstructor_InitializesNestedConfigurations()
    {
        // Arrange
        var config = new TestMapConfig();

        // Act
        var runtimeConfig = config.RuntimeConfig;
        var testingConfig = config.TestingConfig;
        var aiProviderConfig = config.AiProviderConfig;
        var experimentConfig = config.ExperimentConfig;

        // Assert
        Assert.NotNull(runtimeConfig);
        Assert.NotNull(testingConfig);
        Assert.NotNull(aiProviderConfig);
        Assert.NotNull(experimentConfig);
    }

    /// <summary>
    /// Verifies that the generation configuration uses the expected default provider, mode, strategy, and executor values.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GenerationConfig_DefaultConstructor_UsesExpectedDefaults()
    {
        // Arrange
        var config = new GenerationConfig();

        // Act
        var provider = config.Provider;
        var mode = config.Mode;
        var strategy = config.Strategy;
        var objective = config.Objective;
        var metricsPath = config.MetricsPath;
        var executor = config.Executor;
        var budgetMode = config.BudgetMode;
        var temperature = config.Temperature;
        var stepErrorRetries = config.StepErrorRetries;
        var stepRetryDelayMs = config.StepRetryDelayMs;
        var contextMode = config.ContextMode;
        var steps = config.Steps;
        var targetSelection = config.TargetSelection;
        var bootstrap = config.Bootstrap;
        var acceptance = config.Acceptance;

        // Assert
        Assert.Equal(TestGenerationObjective.TestSuiteExpansion, objective);
        Assert.Equal(AiProvider.OpenAi, provider);
        Assert.Equal(AiProviderMode.Chat, mode);
        Assert.Equal(TestGenerationApproach.MetricsDriven, strategy);
        Assert.Equal(MetricsDrivenPath.CoverageAndMutation, metricsPath);
        Assert.Equal(TestActionExecutorMode.BasicExtension, executor);
        Assert.Equal(GenerationBudgetMode.PassAt1RepairAt5, budgetMode);
        Assert.Equal(0.0, temperature);
        Assert.Equal(0, stepErrorRetries);
        Assert.Equal(1000, stepRetryDelayMs);
        Assert.Equal(GenerationContextMode.ChainedHistory, contextMode);
        Assert.NotNull(steps);
        Assert.True(steps.EnableEvidencePackage);
        Assert.False(steps.EnableContextGraph);
        Assert.False(steps.EnableContextResolution);
        Assert.True(steps.EnableScenario);
        Assert.True(steps.EnableMethodName);
        Assert.True(steps.EnableArrangePlan);
        Assert.True(steps.EnableInputPlan);
        Assert.True(steps.EnableActionPlan);
        Assert.True(steps.EnableAssertionPlan);
        Assert.True(steps.EnableFinalTest);
        Assert.NotNull(targetSelection);
        Assert.NotNull(bootstrap);
        Assert.NotNull(acceptance);
        Assert.True(acceptance.RequireCompilationSuccess);
        Assert.True(acceptance.RequireTestsToRun);
        Assert.True(acceptance.RequireAllTestsPass);
        Assert.True(acceptance.RequireCoverageImprovement);
        Assert.Equal(0.0, acceptance.MinCoverageImprovement);
    }

    /// <summary>
    /// Verifies that the experiment configuration uses the expected defaults for shared generation and experiment execution.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ExperimentConfig_DefaultConstructor_UsesExpectedDefaults()
    {
        // Arrange
        var config = new ExperimentConfig();

        // Act
        var generationApproach = config.GenerationApproach;
        var objective = config.Objective;
        var approaches = config.Approaches;
        var metricsPaths = config.MetricsPaths;
        var budgetModes = config.BudgetModes;
        var compareHistoryModes = config.CompareHistoryModes;
        var contextModes = config.ContextModes;
        var stepAblation = config.StepAblation;
        var temperature = config.Temperature;
        var executor = config.Executor;
        var candidateLimit = config.CandidateLimit;
        var minCoverageThreshold = config.MinCoverageThreshold;
        var maxCoverageThreshold = config.MaxCoverageThreshold;
        var includeDetailedErrors = config.IncludeDetailedErrors;
        var stepErrorRetries = config.StepErrorRetries;
        var stepRetryDelayMs = config.StepRetryDelayMs;
        var resume = config.Resume;

        // Assert
        Assert.Equal(TestGenerationObjective.TestSuiteExpansion, objective);
        Assert.Equal(TestGenerationApproach.MetricsDriven, generationApproach);
        Assert.Equal([TestGenerationApproach.MetricsDriven], approaches);
        Assert.Equal([MetricsDrivenPath.CoverageAndMutation], metricsPaths);
        Assert.Equal([GenerationBudgetMode.PassAt1], budgetModes);
        Assert.False(compareHistoryModes);
        Assert.Equal([GenerationContextMode.ChainedHistory], contextModes);
        Assert.NotNull(stepAblation);
        Assert.False(stepAblation.Enabled);
        Assert.True(stepAblation.IncludeBaseline);
        Assert.False(stepAblation.IncludeAllDisabled);
        Assert.Equal(32, stepAblation.MaxVariants);
        Assert.Empty(stepAblation.Steps);
        Assert.Equal(0.0, temperature);
        Assert.Equal(TestActionExecutorMode.BasicExtension, executor);
        Assert.Equal(3, candidateLimit);
        Assert.Equal(0.0, minCoverageThreshold);
        Assert.Equal(0.99, maxCoverageThreshold);
        Assert.True(includeDetailedErrors);
        Assert.Equal(0, stepErrorRetries);
        Assert.Equal(1000, stepRetryDelayMs);
        Assert.True(resume.Enabled);
        Assert.False(resume.RewriteResultsFileOnResume);
    }

    /// <summary>
    /// Verifies that the AI provider configuration exposes each provider in the aggregate provider list.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AiProviderConfig_ProviderConfigs_ContainsAllConfiguredProviders()
    {
        // Arrange
        var config = new AiProviderConfig();

        // Act
        var providers = config.ProviderConfigs.Select(x => x.Provider).ToList();

        // Assert
        Assert.Equal(
            [
                AiProvider.OpenAi,
                AiProvider.Amazon,
                AiProvider.GoogleGemini,
                AiProvider.GoogleCloud,
                AiProvider.CustomOpenAi,
                AiProvider.Ollama
            ],
            providers);
    }
}
