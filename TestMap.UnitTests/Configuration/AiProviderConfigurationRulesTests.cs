using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Amazon;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Models.Configuration.AiProviders.Ollama;
using TestMap.Models.Configuration.AiProviders.OpenAI;
using TestMap.Services.Configuration;

namespace TestMap.UnitTests.Configuration;

public sealed class AiProviderConfigurationRulesTests : IDisposable
{
    private readonly List<EnvironmentVariableScope> _scopes = [];

    /// <summary>
    /// Verifies that providers which require a model fail validation when the model is missing.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(AiProvider.OpenAi)]
    [InlineData(AiProvider.GoogleGemini)]
    [InlineData(AiProvider.GoogleCloud)]
    [InlineData(AiProvider.CustomOpenAi)]
    [InlineData(AiProvider.Ollama)]
    [InlineData(AiProvider.Amazon)]
    public void GetValidationError_MissingModel_ReturnsModelRequirement(AiProvider provider)
    {
        // Arrange
        var providerConfig = CreateProviderConfig(provider);

        // Act
        var error = AiProviderConfigurationRules.GetValidationError(providerConfig);

        // Assert
        Assert.NotNull(error);
        Assert.Contains("requires a non-empty model name", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that API-key based providers are considered usable when required fields are configured.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(AiProvider.OpenAi)]
    [InlineData(AiProvider.GoogleGemini)]
    [InlineData(AiProvider.Amazon)]
    public void IsUsable_RequiredFieldsProvided_ReturnsTrue(AiProvider provider)
    {
        // Arrange
        var providerConfig = CreateProviderConfig(provider);
        providerConfig.Model = "test-model";
        providerConfig.ApiKey = "test-api-key";

        // Act
        var isUsable = AiProviderConfigurationRules.IsUsable(providerConfig);

        // Assert
        Assert.True(isUsable);
    }

    /// <summary>
    /// Verifies that a custom OpenAI provider requires an endpoint after the model requirement is satisfied.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GetValidationError_CustomOpenAiWithoutEndpoint_ReturnsEndpointRequirement()
    {
        // Arrange
        var providerConfig = new CustomOpenAiConfig
        {
            Model = "custom-model",
            ApiKey = "test-api-key",
            Endpoint = string.Empty
        };

        // Act
        var error = AiProviderConfigurationRules.GetValidationError(providerConfig);

        // Assert
        Assert.Equal("Provider 'CustomOpenAi' requires a non-empty endpoint.", error);
    }

    /// <summary>
    /// Verifies that Google Cloud validation fails when no direct or ambient credential source is available.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GetValidationError_GoogleCloudWithoutCredentials_ReturnsCredentialRequirement()
    {
        // Arrange
        AddScope("GOOGLE_CLOUD_ACCESS_TOKEN", null);
        AddScope("GOOGLE_APPLICATION_CREDENTIALS", null);
        var providerConfig = new GoogleCloudConfig
        {
            Model = "gemini-2.5-pro"
        };

        // Act
        var error = AiProviderConfigurationRules.GetValidationError(providerConfig);

        // Assert
        Assert.Equal(
            "Provider 'GoogleCloud' requires an API key, access token, token path, or application default credentials.",
            error);
    }

    /// <summary>
    /// Verifies that Google Cloud validation accepts ambient access-token credentials from the environment.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void IsUsable_GoogleCloudWithEnvironmentCredential_ReturnsTrue()
    {
        // Arrange
        AddScope("GOOGLE_CLOUD_ACCESS_TOKEN", "access-token");
        var providerConfig = new GoogleCloudConfig
        {
            Model = "gemini-2.5-pro"
        };

        // Act
        var isUsable = AiProviderConfigurationRules.IsUsable(providerConfig);

        // Assert
        Assert.True(isUsable);
    }

    /// <summary>
    /// Verifies that Ollama does not require an API key once the model is configured.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void IsUsable_OllamaWithModelAndNoApiKey_ReturnsTrue()
    {
        // Arrange
        var providerConfig = new OllamaConfig
        {
            Model = "llama3.1"
        };

        // Act
        var isUsable = AiProviderConfigurationRules.IsUsable(providerConfig);

        // Assert
        Assert.True(isUsable);
    }

    public void Dispose()
    {
        foreach (var scope in Enumerable.Reverse(_scopes))
        {
            scope.Dispose();
        }
    }

    private void AddScope(string name, string? value)
    {
        _scopes.Add(new EnvironmentVariableScope(name, value));
    }

    private static IAiProviderConfig CreateProviderConfig(AiProvider provider)
    {
        return provider switch
        {
            AiProvider.OpenAi => new OpenAiConfig(),
            AiProvider.GoogleGemini => new GoogleGeminiConfig(),
            AiProvider.GoogleCloud => new GoogleCloudConfig(),
            AiProvider.CustomOpenAi => new CustomOpenAiConfig(),
            AiProvider.Ollama => new OllamaConfig(),
            AiProvider.Amazon => new AmazonConfig(),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }
}
