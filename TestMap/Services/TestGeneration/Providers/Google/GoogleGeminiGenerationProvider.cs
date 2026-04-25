using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Services.TestGeneration.Providers.Google;

public class GoogleGeminiGenerationProvider : SemanticKernelGenerationProviderBase
{
    public override AiProvider Provider => AiProvider.GoogleGemini;

    public override Task CreateAsync(
        IAiProviderConfig providerConfig,
        AiProviderMode mode,
        CancellationToken cancellationToken = default)
    {
        var config = providerConfig as GoogleGeminiConfig
                     ?? throw new InvalidOperationException("Google Gemini config was not provided.");

        if (!string.IsNullOrWhiteSpace(config.Endpoint))
            throw new InvalidOperationException(
                "Semantic Kernel Google Gemini integration does not support custom Gemini endpoints in this configuration.");

        var service = new GoogleAIGeminiChatCompletionService(
            config.Model,
            config.ApiKey,
            GoogleAIVersion.V1_Beta,
            null,
            null);

        Initialize(mode, service, null);
        return Task.CompletedTask;
    }

    protected override PromptExecutionSettings CreateExecutionSettings(double temperature)
    {
        return new GeminiPromptExecutionSettings
        {
            Temperature = temperature
        };
    }
}