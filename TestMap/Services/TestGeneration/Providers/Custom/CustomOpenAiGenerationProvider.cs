using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Services.TestGeneration.Providers.Custom;

public class CustomOpenAiGenerationProvider : SemanticKernelGenerationProviderBase
{
    public override AiProvider Provider => AiProvider.CustomOpenAi;

    public override Task CreateAsync(
        IAiProviderConfig providerConfig,
        AiProviderMode mode,
        CancellationToken cancellationToken = default)
    {
        var config = providerConfig as CustomOpenAiConfig
                     ?? throw new InvalidOperationException("Custom OpenAI config was not provided.");

        // Supplying an HttpClient makes Semantic Kernel drop the SDK's 100-second network timeout
        // and its hidden internal retries, so this timeout governs and step retries are the only
        // retries (and the only ones the usage estimate counts).
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(Math.Max(1, config.RequestTimeoutMinutes))
        };

#pragma warning disable SKEXP0010
        var service = new OpenAIChatCompletionService(
            config.Model,
            new Uri(config.Endpoint),
            config.ApiKey,
            config.OrgId,
            httpClient,
            null);
#pragma warning restore SKEXP0010

        Initialize(mode, service, service);
        return Task.CompletedTask;
    }

    protected override PromptExecutionSettings CreateExecutionSettings(double temperature)
    {
        return new OpenAIPromptExecutionSettings
        {
            Temperature = temperature
        };
    }
}