using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.Configuration;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.UnitTests.Configuration;

public sealed class GenerateConfigurationServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    /// <summary>
    /// Verifies that GenerateConfiguration writes a default config file with the expected runtime paths and core defaults.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GenerateConfiguration_WritesExpectedConfigFile()
    {
        // Arrange
        var basePath = CreateTemporaryDirectory();
        var basePathParent = CreateTemporaryDirectory();
        var configDir = Path.Combine(basePath, "Config");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "default-config.json");
        var service = new GenerateConfigurationService(configPath, basePath, basePathParent);

        // Act
        service.GenerateConfiguration();

        // Assert
        Assert.True(File.Exists(configPath));

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<TestMapConfig>(json, ConfigJsonSerializer.CreateOptions());

        Assert.NotNull(config);
        Assert.Equal(Path.Combine(basePath, "Data", "example_project.txt"), config.RuntimeConfig.FilePaths.TargetFilePath);
        Assert.Equal(Path.Combine(basePath, "Logs"), config.RuntimeConfig.FilePaths.LogsDirPath);
        Assert.Equal(Path.Combine(basePathParent, "Temp"), config.RuntimeConfig.FilePaths.TempDirPath);
        Assert.Equal(Path.Combine(basePath, "Output"), config.RuntimeConfig.FilePaths.OutputDirPath);
        Assert.Equal("desktop-linux", config.RuntimeConfig.Docker.Context);
        Assert.Equal("net-sdk-all", config.RuntimeConfig.Docker.Image);
        Assert.True(config.RuntimeConfig.Project.KeepProjectFiles);
        Assert.Equal(5, config.RuntimeConfig.MaxConcurrency);
        Assert.Equal(TestGenerationObjective.TestSuiteExpansion, config.TestingConfig.GenerationConfig.Objective);
        Assert.Equal(AiProvider.OpenAi, config.TestingConfig.GenerationConfig.Provider);
        Assert.Equal(AiProviderMode.Chat, config.TestingConfig.GenerationConfig.Mode);
        Assert.Equal(TestGenerationApproach.MetricsDriven, config.TestingConfig.GenerationConfig.Strategy);
        Assert.Equal(MetricsDrivenPath.CoverageAndMutation, config.TestingConfig.GenerationConfig.MetricsPath);
        Assert.Equal(TestActionExecutorMode.BasicExtension, config.TestingConfig.GenerationConfig.Executor);
        Assert.Equal(GenerationBudgetMode.PassAt1RepairAt5, config.TestingConfig.GenerationConfig.BudgetMode);
        Assert.Equal(0.0, config.TestingConfig.GenerationConfig.Temperature);
        Assert.Equal(0, config.TestingConfig.GenerationConfig.StepErrorRetries);
        Assert.Equal(1000, config.TestingConfig.GenerationConfig.StepRetryDelayMs);
        Assert.Equal(GenerationContextMode.ChainedHistory, config.TestingConfig.GenerationConfig.ContextMode);
        Assert.True(config.TestingConfig.GenerationConfig.Steps.EnableScenario);
        Assert.False(config.TestingConfig.GenerationConfig.Steps.EnableContextGraph);
        Assert.True(config.TestingConfig.GenerationConfig.Steps.EnableRoslynValidation);
        Assert.Equal(3, config.TestingConfig.TestingFrameworks.Count);
        Assert.Equal("gpt-3.5-turbo", config.AiProviderConfig.OpenAi.Model);
        Assert.Equal("http://localhost:10000/", config.AiProviderConfig.Ollama.Endpoint);
        Assert.Equal("http://localhost:11434/", config.AiProviderConfig.CustomOpenAi.Endpoint);
        Assert.Equal("gemini-1.5-flash", config.AiProviderConfig.GoogleGemini.Model);
        Assert.Equal("us-central1", config.AiProviderConfig.GoogleCloud.Location);
        Assert.Equal(TestGenerationObjective.TestSuiteExpansion, config.ExperimentConfig.Objective);
        Assert.Equal(TargetSelectionStrategy.MetricDrivenImprovement, config.ExperimentConfig.CandidateSelectionStrategy);
        Assert.Equal(TestGenerationApproach.MetricsDriven, config.ExperimentConfig.GenerationApproach);
        Assert.Equal(TestActionExecutorMode.BasicExtension, config.ExperimentConfig.Executor);
        Assert.Equal([TestGenerationApproach.Naive, TestGenerationApproach.MetricsDriven], config.ExperimentConfig.Approaches);
        Assert.Equal(
            [MetricsDrivenPath.Coverage, MetricsDrivenPath.Mutation, MetricsDrivenPath.CoverageAndMutation],
            config.ExperimentConfig.MetricsPaths);
        Assert.Equal([GenerationBudgetMode.PassAt1, GenerationBudgetMode.PassAt5], config.ExperimentConfig.BudgetModes);
        Assert.True(config.ExperimentConfig.CompareHistoryModes);
        Assert.Equal([GenerationContextMode.ChainedHistory], config.ExperimentConfig.ContextModes);
        Assert.False(config.ExperimentConfig.StepAblation.Enabled);
        Assert.True(config.ExperimentConfig.StepAblation.IncludeBaseline);
        Assert.False(config.ExperimentConfig.StepAblation.IncludeAllDisabled);
        Assert.Equal(32, config.ExperimentConfig.StepAblation.MaxVariants);
        Assert.Contains(GenerationStepType.ContextGraph, config.ExperimentConfig.StepAblation.Steps);
        Assert.Contains(GenerationStepType.RoslynValidation, config.ExperimentConfig.StepAblation.Steps);
        Assert.Equal(Path.Combine(basePath, "Output"), config.ExperimentConfig.OutputPath);
        Assert.True(config.ExperimentConfig.Resume.Enabled);
        Assert.False(config.ExperimentConfig.Resume.RewriteResultsFileOnResume);
    }

    public void Dispose()
    {
        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }
}
