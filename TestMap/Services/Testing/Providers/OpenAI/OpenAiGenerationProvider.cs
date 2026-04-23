using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.OpenAI;
using TestMap.Services.Testing.Providers.Abstractions;

namespace TestMap.Services.Testing.Providers.OpenAI;

public class OpenAiGenerationProvider : SemanticKernelGenerationProviderBase
{
    public override AiProvider Provider => AiProvider.OpenAi;

    public override Task CreateAsync(
        IAiProviderConfig providerConfig,
        AiProviderMode mode,
        CancellationToken cancellationToken = default)
    {
        var config = providerConfig as OpenAiConfig
                     ?? throw new InvalidOperationException("OpenAI config was not provided.");

        var service = new OpenAIChatCompletionService(
            config.Model,
            config.ApiKey,
            config.OrgId,
            null,
            null);

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
