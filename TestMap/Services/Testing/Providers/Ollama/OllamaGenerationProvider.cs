using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.TextGeneration;
using Microsoft.Extensions.AI;
using OllamaSharp;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Ollama;
using TestMap.Services.Testing.Providers.Abstractions;

namespace TestMap.Services.Testing.Providers.Ollama;

public class OllamaGenerationProvider : SemanticKernelGenerationProviderBase
{
    public override AiProvider Provider => AiProvider.Ollama;

    public override Task CreateAsync(
        IAiProviderConfig providerConfig,
        AiProviderMode mode,
        CancellationToken cancellationToken = default)
    {
        var config = providerConfig as OllamaConfig
                     ?? throw new InvalidOperationException("Ollama config was not provided.");

        var endpoint = new Uri(config.Endpoint);
        var client = new OllamaApiClient(endpoint, config.Model);
        var chatService = ((IChatClient)client).AsChatCompletionService();
        var textService = new OllamaTextGenerationService(client, null);

        Initialize(mode, chatService, textService);
        return Task.CompletedTask;
    }

    protected override PromptExecutionSettings CreateExecutionSettings(double temperature)
    {
        return new OllamaPromptExecutionSettings
        {
            Temperature = (float)temperature
        };
    }
}
