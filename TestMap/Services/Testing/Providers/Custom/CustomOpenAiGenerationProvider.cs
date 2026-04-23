using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Services.Testing.Providers.Abstractions;

namespace TestMap.Services.Testing.Providers.Custom;

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

#pragma warning disable SKEXP0010
        var service = new OpenAIChatCompletionService(
            config.Model,
            new Uri(config.Endpoint),
            config.ApiKey,
            config.OrgId,
            null,
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
