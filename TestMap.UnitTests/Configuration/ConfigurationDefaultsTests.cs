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
        var executor = config.Executor;
        var maxRetries = config.MaxRetries;
        var targetSelection = config.TargetSelection;
        var bootstrap = config.Bootstrap;

        // Assert
        Assert.Equal(AiProvider.OpenAi, provider);
        Assert.Equal(AiProviderMode.Chat, mode);
        Assert.Equal(TestGenerationApproach.ActionAware, strategy);
        Assert.Equal(TestActionExecutorMode.ActionAware, executor);
        Assert.Equal(1, maxRetries);
        Assert.NotNull(targetSelection);
        Assert.NotNull(bootstrap);
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
        var executor = config.Executor;
        var candidateLimit = config.CandidateLimit;
        var minCoverageThreshold = config.MinCoverageThreshold;
        var maxCoverageThreshold = config.MaxCoverageThreshold;
        var includeDetailedErrors = config.IncludeDetailedErrors;
        var stepErrorRetries = config.StepErrorRetries;
        var stepRetryDelayMs = config.StepRetryDelayMs;
        var strategies = config.Strategies;

        // Assert
        Assert.Equal(TestGenerationApproach.DefaultCoverageExtension, generationApproach);
        Assert.Equal(TestActionExecutorMode.BasicCoverageExtension, executor);
        Assert.Equal(3, candidateLimit);
        Assert.Equal(0.0, minCoverageThreshold);
        Assert.Equal(0.99, maxCoverageThreshold);
        Assert.True(includeDetailedErrors);
        Assert.Equal(0, stepErrorRetries);
        Assert.Equal(1000, stepRetryDelayMs);
        Assert.Equal(
            [GenerationStrategy.Pass1, GenerationStrategy.Pass5, GenerationStrategy.Repair5],
            strategies);
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
