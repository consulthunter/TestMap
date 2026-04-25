using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Models.Configuration.AiProviders.Google;

namespace TestMap.Services.Configuration;

public static class AiProviderConfigurationRules
{
    public static bool IsUsable(IAiProviderConfig providerConfig)
    {
        return GetValidationError(providerConfig) == null;
    }

    public static string? GetValidationError(IAiProviderConfig providerConfig)
    {
        if (string.IsNullOrWhiteSpace(providerConfig.Model))
            return $"Provider '{providerConfig.Provider}' requires a non-empty model name.";

        if (providerConfig is GoogleCloudConfig googleCloudConfig)
        {
            var hasCredential = !string.IsNullOrWhiteSpace(googleCloudConfig.ApiKey) ||
                                !string.IsNullOrWhiteSpace(googleCloudConfig.AccessToken) ||
                                !string.IsNullOrWhiteSpace(googleCloudConfig.TokenPath) ||
                                !string.IsNullOrWhiteSpace(
                                    Environment.GetEnvironmentVariable("GOOGLE_CLOUD_ACCESS_TOKEN")) ||
                                !string.IsNullOrWhiteSpace(
                                    Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));

            return hasCredential
                ? null
                : "Provider 'GoogleCloud' requires an API key, access token, token path, or application default credentials.";
        }

        if (providerConfig is CustomOpenAiConfig customOpenAiConfig)
            return string.IsNullOrWhiteSpace(customOpenAiConfig.Endpoint)
                ? "Provider 'CustomOpenAi' requires a non-empty endpoint."
                : null;

        if (providerConfig.Provider == AiProvider.Ollama) return null;

        return string.IsNullOrWhiteSpace(providerConfig.ApiKey)
            ? $"Provider '{providerConfig.Provider}' requires a non-empty API key."
            : null;
    }
}