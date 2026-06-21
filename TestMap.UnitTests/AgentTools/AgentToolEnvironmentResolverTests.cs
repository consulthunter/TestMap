using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Models.Configuration.AiProviders.OpenAI;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.AgentTools;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="AgentToolEnvironmentResolver"/> covering provider normalization,
/// model override precedence, missing required secrets, and security invariants.
/// </summary>
public sealed class AgentToolEnvironmentResolverTests
{
    private static AgentToolEnvironmentResolver MakeResolver() => new();

    private static AiProviderConfig MakeProviders(
        string openAiKey = "sk-test",
        string openAiModel = "gpt-4o",
        string customEndpoint = "https://my.openai.endpoint/",
        string customModel = "gpt-4-custom") =>
        new()
        {
            OpenAi = new OpenAiConfig { ApiKey = openAiKey, Model = openAiModel },
            CustomOpenAi = new CustomOpenAiConfig
            {
                ApiKey = "custom-key",
                Model = customModel,
                Endpoint = customEndpoint
            }
        };

    private static GenerationConfig MakeGenerationConfig(
        AiProvider provider = AiProvider.OpenAi,
        string model = "gpt-4o") =>
        new() { Provider = provider };

    /// <summary>
    /// OpenAI provider emits TESTMAP_LLM_PROVIDER=openai, TESTMAP_LLM_MODEL, and
    /// TESTMAP_LLM_API_KEY in NormalizedVars.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_OpenAiProvider_EmitsNormalizedVars()
    {
        // Arrange
        var resolver = MakeResolver();
        var tool = new ExperimentToolConfig { Id = "codex" };
        var providers = MakeProviders(openAiKey: "sk-test-key", openAiModel: "gpt-4o");
        var genConfig = MakeGenerationConfig(AiProvider.OpenAi);

        // Act
        var result = resolver.Resolve(tool, providers, genConfig);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("openai", result.NormalizedVars["TESTMAP_LLM_PROVIDER"]);
        Assert.Equal("gpt-4o", result.NormalizedVars["TESTMAP_LLM_MODEL"]);
        Assert.Equal("sk-test-key", result.NormalizedVars["TESTMAP_LLM_API_KEY"]);
    }

    /// <summary>
    /// Tool-level Model override takes precedence over the provider config model.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ToolModelOverride_TakesPrecedenceOverProviderModel()
    {
        // Arrange
        var resolver = MakeResolver();
        var tool = new ExperimentToolConfig { Id = "codex", Model = "o3" };
        var providers = MakeProviders(openAiModel: "gpt-4o");
        var genConfig = MakeGenerationConfig(AiProvider.OpenAi);

        // Act
        var result = resolver.Resolve(tool, providers, genConfig);

        // Assert
        Assert.Equal("o3", result.NormalizedVars["TESTMAP_LLM_MODEL"]);
    }

    /// <summary>
    /// Tool-level Provider override takes precedence over the generation config provider.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ToolProviderOverride_TakesPrecedenceOverGenConfigProvider()
    {
        // Arrange
        var resolver = MakeResolver();
        // Tool overrides to Anthropic; gen config says OpenAI
        var tool = new ExperimentToolConfig { Id = "claude", Provider = AiProvider.Anthropic };
        var providers = new AiProviderConfig
        {
            OpenAi = new OpenAiConfig { ApiKey = "sk-openai", Model = "gpt-4o" },
            Anthropic = new Models.Configuration.AiProviders.Anthropic.AnthropicConfig
            {
                ApiKey = "sk-ant-test",
                Model = "claude-opus-4"
            }
        };
        var genConfig = MakeGenerationConfig(AiProvider.OpenAi);

        // Act
        var result = resolver.Resolve(tool, providers, genConfig);

        // Assert
        Assert.Equal("anthropic", result.NormalizedVars["TESTMAP_LLM_PROVIDER"]);
        Assert.Equal("sk-ant-test", result.NormalizedVars["TESTMAP_LLM_API_KEY"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ProviderConfigKeyEmpty_UsesCanonicalHostEnvironmentVariable()
    {
        // Arrange
        var resolver = MakeResolver();
        var previous = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-host-anthropic");
        try
        {
            var tool = new ExperimentToolConfig { Id = "aider", Provider = AiProvider.Anthropic };
            var providers = new AiProviderConfig
            {
                Anthropic = new Models.Configuration.AiProviders.Anthropic.AnthropicConfig
                {
                    ApiKey = string.Empty,
                    Model = "claude-sonnet-4-5"
                }
            };
            var genConfig = MakeGenerationConfig(AiProvider.Anthropic);

            // Act
            var result = resolver.Resolve(tool, providers, genConfig);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal("sk-host-anthropic", result.NormalizedVars["TESTMAP_LLM_API_KEY"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previous);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_AnthropicProviderConfigKeyEmpty_UsesAnthropicKeyAlias()
    {
        // Arrange
        var resolver = MakeResolver();
        var previousCanonical = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var previousAlias = Environment.GetEnvironmentVariable("ANTHROPIC_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        Environment.SetEnvironmentVariable("ANTHROPIC_KEY", "sk-host-anthropic-alias");
        try
        {
            var tool = new ExperimentToolConfig { Id = "openhands", Provider = AiProvider.Anthropic };
            var providers = new AiProviderConfig
            {
                Anthropic = new Models.Configuration.AiProviders.Anthropic.AnthropicConfig
                {
                    ApiKey = string.Empty,
                    Model = "claude-sonnet-4-5"
                }
            };
            var genConfig = MakeGenerationConfig(AiProvider.Anthropic);

            // Act
            var result = resolver.Resolve(tool, providers, genConfig);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal("sk-host-anthropic-alias", result.NormalizedVars["TESTMAP_LLM_API_KEY"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previousCanonical);
            Environment.SetEnvironmentVariable("ANTHROPIC_KEY", previousAlias);
        }
    }

    /// <summary>
    /// Names of missing required env vars appear in MissingRequiredSecrets without their values.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_MissingRequiredEnvVar_AppearsInMissingRequiredSecrets()
    {
        // Arrange
        var resolver = MakeResolver();
        var missingVar = $"TESTMAP_MISSING_VAR_{Guid.NewGuid():N}";
        var tool = new ExperimentToolConfig
        {
            Id = "codex",
            RequiredEnvironmentVariables = [missingVar]
        };
        var providers = MakeProviders();
        var genConfig = MakeGenerationConfig();

        // Act
        var result = resolver.Resolve(tool, providers, genConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(missingVar, result.MissingRequiredSecrets);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_CopilotTokenFromHost_EmitsContainerOnlySecret()
    {
        // Arrange
        var resolver = MakeResolver();
        var previous = Environment.GetEnvironmentVariable("GITHUB_COPILOT_TOKEN");
        Environment.SetEnvironmentVariable("GITHUB_COPILOT_TOKEN", "copilot-token");
        try
        {
            var tool = new ExperimentToolConfig
            {
                Id = "copilot",
                RequiredEnvironmentVariables = ["GITHUB_COPILOT_TOKEN"]
            };
            var providers = MakeProviders();
            var genConfig = MakeGenerationConfig();

            // Act
            var result = resolver.Resolve(tool, providers, genConfig);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal("copilot-token", result.NormalizedVars["GITHUB_COPILOT_TOKEN"]);
            Assert.DoesNotContain(result.PersistableMetadata, kvp => kvp.Value == "copilot-token");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_COPILOT_TOKEN", previous);
        }
    }

    /// <summary>
    /// PersistableMetadata never contains any value that matches the API key.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_PersistableMetadata_DoesNotContainApiKeyValue()
    {
        // Arrange
        var resolver = MakeResolver();
        var apiKey = "sk-super-secret-key-12345";
        var tool = new ExperimentToolConfig { Id = "codex" };
        var providers = MakeProviders(openAiKey: apiKey);
        var genConfig = MakeGenerationConfig();

        // Act
        var result = resolver.Resolve(tool, providers, genConfig);

        // Assert — no persisted value should equal the raw API key
        foreach (var kv in result.PersistableMetadata)
            Assert.NotEqual(apiKey, kv.Value);
    }

    /// <summary>
    /// CustomOpenAi provider emits TESTMAP_LLM_BASE_URL from the Endpoint config.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_CustomOpenAiProvider_EmitsBaseUrl()
    {
        // Arrange
        var resolver = MakeResolver();
        var endpoint = "https://my.custom.openai.endpoint/v1/";
        var tool = new ExperimentToolConfig { Id = "codex", Provider = AiProvider.CustomOpenAi };
        var providers = MakeProviders(customEndpoint: endpoint);
        var genConfig = MakeGenerationConfig(AiProvider.CustomOpenAi);

        // Act
        var result = resolver.Resolve(tool, providers, genConfig);

        // Assert
        Assert.Equal("openai", result.NormalizedVars["TESTMAP_LLM_PROVIDER"]);
        Assert.True(result.NormalizedVars.ContainsKey("TESTMAP_LLM_BASE_URL"));
        Assert.Equal(endpoint, result.NormalizedVars["TESTMAP_LLM_BASE_URL"]);
        Assert.Equal(endpoint, result.PersistableMetadata["base_url"]);
    }

    /// <summary>
    /// PersistableMetadata contains provider_id and model but not TESTMAP_LLM_API_KEY.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_PersistableMetadata_ContainsProviderIdAndModelButNotApiKey()
    {
        // Arrange
        var resolver = MakeResolver();
        var tool = new ExperimentToolConfig { Id = "codex" };
        var providers = MakeProviders(openAiKey: "sk-secret", openAiModel: "gpt-4o");
        var genConfig = MakeGenerationConfig();

        // Act
        var result = resolver.Resolve(tool, providers, genConfig);

        // Assert
        Assert.Contains("provider_id", result.PersistableMetadata.Keys);
        Assert.Contains("model", result.PersistableMetadata.Keys);
        Assert.DoesNotContain("api_key", result.PersistableMetadata.Keys);
        Assert.DoesNotContain("TESTMAP_LLM_API_KEY", result.PersistableMetadata.Keys);
    }

    /// <summary>
    /// Non-secret tool environment vars are copied into ToolVars.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ToolEnvironmentVars_AreCopiedToToolVars()
    {
        // Arrange
        var resolver = MakeResolver();
        var tool = new ExperimentToolConfig
        {
            Id = "codex",
            Environment = new Dictionary<string, string>
            {
                ["CODEX_TIMEOUT"] = "300",
                ["CODEX_VERBOSE"] = "true"
            }
        };
        var providers = MakeProviders();
        var genConfig = MakeGenerationConfig();

        // Act
        var result = resolver.Resolve(tool, providers, genConfig);

        // Assert
        Assert.Equal("300", result.ToolVars["CODEX_TIMEOUT"]);
        Assert.Equal("true", result.ToolVars["CODEX_VERBOSE"]);
    }
}
