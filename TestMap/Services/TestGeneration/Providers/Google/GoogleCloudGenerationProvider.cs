using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Services.TestGeneration.Providers.Google;

public class GoogleCloudGenerationProvider : SemanticKernelGenerationProviderBase
{
    public override AiProvider Provider => AiProvider.GoogleCloud;

    public override Task CreateAsync(
        IAiProviderConfig providerConfig,
        AiProviderMode mode,
        CancellationToken cancellationToken = default)
    {
        var config = providerConfig as GoogleCloudConfig
                     ?? throw new InvalidOperationException("Google Cloud config was not provided.");

        if (!string.IsNullOrWhiteSpace(config.Endpoint))
            throw new InvalidOperationException(
                "Semantic Kernel Vertex AI integration does not support custom Vertex endpoints in this configuration.");

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException(
                "Semantic Kernel Vertex AI integration requires bearer-token based authentication. ApiKey-only Vertex configuration is not supported.");

        var tokenProvider = new VertexAiTokenProvider();
        var service = new VertexAIGeminiChatCompletionService(
            config.Model,
            () => new ValueTask<string>(tokenProvider.GetTokenAsync(config, cancellationToken)),
            config.Location,
            config.ProjectId,
            VertexAIVersion.V1,
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