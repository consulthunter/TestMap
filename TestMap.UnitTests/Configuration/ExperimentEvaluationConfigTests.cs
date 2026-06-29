using System.Text.Json;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.Configuration;

namespace TestMap.UnitTests.Configuration;

/// <summary>
/// Tests for <see cref="ExperimentEvaluationConfig"/>, <see cref="GenerationProducerConfig"/>,
/// and <see cref="ExperimentToolConfig"/> covering defaults and JSON round-trips.
/// </summary>
public sealed class ExperimentEvaluationConfigTests
{
    private static JsonSerializerOptions Options => ConfigJsonSerializer.CreateOptions();

    /// <summary>
    /// Default ExperimentEvaluationConfig has TestMap lane enabled and Tools lane disabled with
    /// an empty ToolIds list.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ExperimentEvaluationConfig_Defaults_AreSafe()
    {
        // Arrange / Act
        var config = new ExperimentEvaluationConfig();

        // Assert
        Assert.True(config.TestMap.Enabled);
        Assert.False(config.Tools.Enabled);
        Assert.Empty(config.Tools.ToolIds);
    }

    /// <summary>
    /// Default GenerationProducerConfig has Mode = TestMap and ToolId = null.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GenerationProducerConfig_Defaults_AreCorrect()
    {
        // Arrange / Act
        var config = new GenerationProducerConfig();

        // Assert
        Assert.Equal(TestGenerationProducerMode.TestMap, config.Mode);
        Assert.Null(config.ToolId);
    }

    /// <summary>
    /// Default ExperimentToolConfig has TimeoutMinutes = 45, empty dicts, and empty lists.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ExperimentToolConfig_Defaults_AreCorrect()
    {
        // Arrange / Act
        var config = new ExperimentToolConfig();

        // Assert
        Assert.Equal(45, config.TimeoutMinutes);
        Assert.Empty(config.Environment);
        Assert.Empty(config.RequiredEnvironmentVariables);
        Assert.Empty(config.Metadata);
        Assert.Null(config.Provider);
        Assert.Null(config.Model);
        Assert.Null(config.Endpoint);
        Assert.Null(config.ImageKey);
    }

    /// <summary>
    /// ExperimentEvaluationConfig round-trips through JSON without data loss.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ExperimentEvaluationConfig_JsonRoundTrip_PreservesFields()
    {
        // Arrange
        var config = new ExperimentEvaluationConfig
        {
            TestMap = new TestMapEvaluationConfig(),
            Tools = new ToolEvaluationConfig
            {
                Enabled = true,
                ToolIds = ["codex", "claude"],
                RequireAvailabilityInSetup = false
            }
        };

        // Act
        var json = JsonSerializer.Serialize(config, Options);
        var deserialized = JsonSerializer.Deserialize<ExperimentEvaluationConfig>(json, Options);

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(deserialized!.Tools.Enabled);
        Assert.Equal(2, deserialized.Tools.ToolIds.Count);
        Assert.Contains("codex", deserialized.Tools.ToolIds);
        Assert.Contains("claude", deserialized.Tools.ToolIds);
        Assert.False(deserialized.Tools.RequireAvailabilityInSetup);
    }

    /// <summary>
    /// ExperimentToolConfig round-trips through JSON including provider, model, and env vars.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ExperimentToolConfig_JsonRoundTrip_PreservesFields()
    {
        // Arrange
        var config = new ExperimentToolConfig
        {
            Id = "codex",
            ImageKey = "codex",
            Provider = AiProvider.OpenAi,
            Model = "gpt-4o",
            Endpoint = "https://models.example.test/v1/",
            TimeoutMinutes = 60,
            Environment = new Dictionary<string, string> { ["KEY"] = "VALUE" },
            RequiredEnvironmentVariables = ["OPENAI_API_KEY"],
            Metadata = new Dictionary<string, string> { ["region"] = "us-east-1" }
        };

        // Act
        var json = JsonSerializer.Serialize(config, Options);
        var deserialized = JsonSerializer.Deserialize<ExperimentToolConfig>(json, Options);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("codex", deserialized!.Id);
        Assert.Equal("codex", deserialized.ImageKey);
        Assert.Equal(AiProvider.OpenAi, deserialized.Provider);
        Assert.Equal("gpt-4o", deserialized.Model);
        Assert.Equal("https://models.example.test/v1/", deserialized.Endpoint);
        Assert.Equal(60, deserialized.TimeoutMinutes);
        Assert.Equal("VALUE", deserialized.Environment["KEY"]);
        Assert.Contains("OPENAI_API_KEY", deserialized.RequiredEnvironmentVariables);
        Assert.Equal("us-east-1", deserialized.Metadata["region"]);
    }

    /// <summary>
    /// GenerationProducerConfig round-trips through JSON with Mode and ToolId set.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GenerationProducerConfig_JsonRoundTrip_PreservesFields()
    {
        // Arrange
        var config = new GenerationProducerConfig
        {
            Mode = TestGenerationProducerMode.AgentTool,
            ToolId = "codex"
        };

        // Act
        var json = JsonSerializer.Serialize(config, Options);
        var deserialized = JsonSerializer.Deserialize<GenerationProducerConfig>(json, Options);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(TestGenerationProducerMode.AgentTool, deserialized!.Mode);
        Assert.Equal("codex", deserialized.ToolId);
    }
}
