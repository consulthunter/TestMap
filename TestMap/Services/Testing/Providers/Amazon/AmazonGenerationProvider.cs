using Amazon;
using Amazon.BedrockRuntime;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Amazon;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Amazon;
using TestMap.Services.Testing.Providers.Abstractions;

namespace TestMap.Services.Testing.Providers.Amazon;

public class AmazonGenerationProvider : SemanticKernelGenerationProviderBase
{
    private string _modelId = string.Empty;

    public override AiProvider Provider => AiProvider.Amazon;

    public override Task CreateAsync(
        IAiProviderConfig providerConfig,
        AiProviderMode mode,
        CancellationToken cancellationToken = default)
    {
        var config = providerConfig as AmazonConfig
                     ?? throw new InvalidOperationException("Amazon config was not provided.");

        _modelId = config.Model;
        var runtime = new AmazonBedrockRuntimeClient(
            config.AwsAccessKey,
            config.ApiKey,
            RegionEndpoint.GetBySystemName(config.AwsRegion));

        var chatService = new BedrockChatCompletionService(config.Model, runtime, null);
        var textService = new BedrockTextGenerationService(config.Model, runtime, null);

        Initialize(mode, chatService, textService);
        return Task.CompletedTask;
    }

    protected override PromptExecutionSettings CreateExecutionSettings(double temperature)
    {
        if (_modelId.Contains("claude", StringComparison.OrdinalIgnoreCase))
        {
            return new AmazonClaudeExecutionSettings { Temperature = (float)temperature };
        }

        if (_modelId.Contains("command-r", StringComparison.OrdinalIgnoreCase))
        {
            return new AmazonCommandRExecutionSettings { Temperature = (float)temperature };
        }

        if (_modelId.Contains("command", StringComparison.OrdinalIgnoreCase))
        {
            return new AmazonCommandExecutionSettings { Temperature = (float)temperature };
        }

        if (_modelId.Contains("jamba", StringComparison.OrdinalIgnoreCase))
        {
            return new AmazonJambaExecutionSettings { Temperature = (float)temperature };
        }

        if (_modelId.Contains("llama", StringComparison.OrdinalIgnoreCase))
        {
            return new AmazonLlama3ExecutionSettings { Temperature = (float)temperature };
        }

        if (_modelId.Contains("mistral", StringComparison.OrdinalIgnoreCase))
        {
            return new AmazonMistralExecutionSettings { Temperature = (float)temperature };
        }

        if (_modelId.Contains("titan", StringComparison.OrdinalIgnoreCase))
        {
            return new AmazonTitanExecutionSettings { Temperature = (float)temperature };
        }

        return new AmazonClaudeExecutionSettings { Temperature = (float)temperature };
    }
}
