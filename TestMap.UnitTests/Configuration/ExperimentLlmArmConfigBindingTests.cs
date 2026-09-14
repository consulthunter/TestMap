using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Services.Configuration;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.Configuration;

/// <summary>
/// Binding tests for ExperimentConfig.LlmArms. These go through the real config serializer
/// options so the JSON a user writes is the JSON that is proven to bind.
/// </summary>
public sealed class ExperimentLlmArmConfigBindingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_LlmArms_BindsIdProviderModelAndEndpoint()
    {
        const string json = """
        {
          "ExperimentConfig": {
            "LlmArms": [
              { "Id": "custom-glm", "Provider": "CustomOpenAi", "Model": "GLM-5.3" },
              { "Id": "custom-gpt-oss", "Provider": "CustomOpenAi", "Model": "gpt-oss-120b" },
              {
                "Id": "custom-kimi",
                "Provider": "CustomOpenAi",
                "Model": "Kimi-K3",
                "Endpoint": "https://llm-api.example.test/api/"
              }
            ]
          }
        }
        """;

        var config = JsonSerializer.Deserialize<TestMapConfig>(json, ConfigJsonSerializer.CreateOptions());

        var arms = config!.ExperimentConfig.LlmArms;
        Assert.Equal(3, arms.Count);
        Assert.All(arms, x => Assert.Equal(AiProvider.CustomOpenAi, x.Provider));
        Assert.Equal(["custom-glm", "custom-gpt-oss", "custom-kimi"], arms.Select(x => x.Id).ToArray());
        Assert.Equal(["GLM-5.3", "gpt-oss-120b", "Kimi-K3"], arms.Select(x => x.Model ?? string.Empty).ToArray());
        Assert.Null(arms[0].Endpoint);
        Assert.Equal("https://llm-api.example.test/api/", arms[2].Endpoint);
    }

    /// <summary>
    /// Every config in the repo today omits LlmArms; it must bind to an empty list rather than
    /// null, which is what routes those configs down the legacy provider expansion.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_WithoutLlmArms_YieldsEmptyList()
    {
        var config = JsonSerializer.Deserialize<TestMapConfig>(
            """{ "ExperimentConfig": { "IncludeProviders": ["OpenAi"] } }""",
            ConfigJsonSerializer.CreateOptions());

        Assert.NotNull(config!.ExperimentConfig.LlmArms);
        Assert.Empty(config.ExperimentConfig.LlmArms);
        Assert.Equal(["OpenAi"], config.ExperimentConfig.IncludeProviders);
    }

    /// <summary>
    /// Provider is optional on an arm and falls back to the generation provider.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_ArmWithoutProvider_LeavesProviderNull()
    {
        var config = JsonSerializer.Deserialize<TestMapConfig>(
            """{ "ExperimentConfig": { "LlmArms": [ { "Id": "a", "Model": "m" } ] } }""",
            ConfigJsonSerializer.CreateOptions());

        var arm = Assert.Single(config!.ExperimentConfig.LlmArms);
        Assert.Null(arm.Provider);
        Assert.Equal("m", arm.Model);
    }

    /// <summary>
    /// Every config shipped in the repository must bind and pass matrix validation. Arms replace
    /// provider selection, so any config declaring them must not also carry IncludeProviders or
    /// PreferredProvider — this is the check that catches a half-converted config before a run
    /// fails at startup.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("default-config.json")]
    [InlineData("pilot-config.json")]
    [InlineData("copilot-tool-experiment.json")]
    public void Deserialize_RepositoryConfig_BindsAndValidates(string fileName)
    {
        var experiment = LoadConfig(fileName).ExperimentConfig;

        if (experiment.LlmArms.Count > 0)
        {
            Assert.Empty(experiment.IncludeProviders);
            Assert.True(string.IsNullOrWhiteSpace(experiment.PreferredProvider));
        }

        ExperimentConfigurationValidator.ValidateMatrixSettings(experiment);
    }

    /// <summary>
    /// The pilot config drives the LLM lane entirely from arms: three hosted providers on their
    /// configured models plus three open models behind the one custom endpoint. Arms replace
    /// provider selection, so IncludeProviders and PreferredProvider must be absent or the run
    /// fails validation at startup.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Deserialize_PilotConfig_ArmsBindAndValidate()
    {
        var experiment = LoadConfig("pilot-config.json").ExperimentConfig;

        Assert.Empty(experiment.IncludeProviders);
        Assert.True(string.IsNullOrWhiteSpace(experiment.PreferredProvider));
        Assert.Equal(
            ["openai", "amazon", "gemini", "custom-glm", "custom-kimi", "custom-gpt-oss"],
            experiment.LlmArms.Select(x => x.Id).ToArray());

        // The three custom arms share one provider and differ only by model — the case arms exist
        // for, and the case that previously required three separate experiment runs.
        var custom = experiment.LlmArms.Where(x => x.Provider == AiProvider.CustomOpenAi).ToList();
        Assert.Equal(3, custom.Count);
        Assert.Equal(3, custom.Select(x => x.Model).Distinct().Count());

        // Arms without a model inherit their provider's configured one.
        Assert.All(
            experiment.LlmArms.Where(x => x.Provider != AiProvider.CustomOpenAi),
            x => Assert.True(string.IsNullOrWhiteSpace(x.Model)));

        ExperimentConfigurationValidator.ValidateMatrixSettings(experiment);
    }

    private static TestMapConfig LoadConfig(string fileName)
    {
        var config = JsonSerializer.Deserialize<TestMapConfig>(
            File.ReadAllText(ResolveConfigPath(fileName)),
            ConfigJsonSerializer.CreateOptions());

        Assert.NotNull(config);
        return config!;
    }

    private static string ResolveConfigPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "TestMap", "Config", fileName);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate TestMap/Config/{fileName}.");
    }
}
