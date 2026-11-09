using Anthropic;
using Anthropic.Models.Messages;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.AiProviders.Anthropic;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Services.TestGeneration.Providers.Anthropic;

/// <summary>
/// Calls the Anthropic Messages API through the official SDK rather than Semantic Kernel. Sends the
/// same system prompt and user message as the Semantic Kernel providers, so the pipeline's local
/// token estimates stay comparable across arms.
/// </summary>
public class AnthropicGenerationProvider : IAiGenerationProvider
{
    // Non-streaming requests: large enough that a generated test method is never cut off, small
    // enough to stay inside the SDK's HTTP timeout.
    internal const int MaxOutputTokens = 16000;

    // Models that reject sampling parameters: temperature, top_p and top_k return a 400 on these.
    private static readonly string[] ModelsWithoutSamplingParameters =
    [
        "claude-fable-",
        "claude-mythos-",
        "claude-opus-5",
        "claude-opus-4-8",
        "claude-opus-4-7",
        "claude-sonnet-5",
    ];

    private AnthropicClient? _client;
    private string _clientApiKey = string.Empty;
    private string _model = string.Empty;
    private AiProviderMode _mode;

    public AiProvider Provider => AiProvider.Anthropic;

    public Task CreateAsync(
        IAiProviderConfig providerConfig,
        AiProviderMode mode,
        CancellationToken cancellationToken = default)
    {
        var config = providerConfig as AnthropicConfig
                     ?? throw new InvalidOperationException("Anthropic config was not provided.");
        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("Anthropic config requires a model.");

        // The pipeline calls CreateAsync before every attempt; keep one client per key so its
        // connection pool is reused. A blank key falls back to the SDK's ANTHROPIC_API_KEY lookup.
        if (_client == null || _clientApiKey != config.ApiKey)
        {
            _client = string.IsNullOrWhiteSpace(config.ApiKey)
                ? new AnthropicClient()
                : new AnthropicClient { ApiKey = config.ApiKey };
            _clientApiKey = config.ApiKey;
        }

        _model = config.Model;
        _mode = mode;
        return Task.CompletedTask;
    }

    public async Task<string> GenerateAsync(
        string prompt,
        double temperature = 0.0,
        CancellationToken cancellationToken = default)
    {
        if (_client == null) throw new InvalidOperationException("Provider was not initialized.");

        var request = BuildRequest(
            _model,
            prompt,
            _mode == AiProviderMode.Chat ? SemanticKernelGenerationProviderBase.DefaultSystemPrompt : null,
            AcceptsSamplingParameters(_model) ? temperature : null);

        var response = await _client.Messages.Create(request, cancellationToken);

        // A refusal has no usable text; surface it as a failure rather than an empty test.
        if (response.StopReason == "refusal")
            throw new InvalidOperationException(
                $"Anthropic declined the request (category: {response.StopDetails?.Category?.ToString() ?? "unspecified"}).");

        return string.Concat(response.Content.Select(block => block.Value).OfType<TextBlock>().Select(block => block.Text));
    }

    public IReadOnlyList<string> GetTokenizableInputSegments(string prompt) =>
        _mode == AiProviderMode.Chat
            ? [SemanticKernelGenerationProviderBase.DefaultSystemPrompt, prompt]
            : [prompt];

    internal static bool AcceptsSamplingParameters(string model) =>
        !ModelsWithoutSamplingParameters.Any(prefix => model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds the request so optional fields are left unset rather than assigned null: a model
    /// that forbids sampling parameters must not receive a temperature field at all.
    /// </summary>
    internal static MessageCreateParams BuildRequest(
        string model,
        string prompt,
        string? systemPrompt,
        double? temperature)
    {
        List<MessageParam> messages = [new() { Role = Role.User, Content = prompt }];

        // The SDK marks Temperature obsolete because newer models reject it. Older models such as
        // Haiku 4.5 still honour it, and the arms depend on that, so it is set only for them.
#pragma warning disable CS0618
        return (systemPrompt, temperature) switch
        {
            ({ } system, { } temp) => new MessageCreateParams
            {
                Model = model, MaxTokens = MaxOutputTokens, Messages = messages,
                System = system, Temperature = temp,
            },
            ({ } system, null) => new MessageCreateParams
            {
                Model = model, MaxTokens = MaxOutputTokens, Messages = messages,
                System = system,
            },
            (null, { } temp) => new MessageCreateParams
            {
                Model = model, MaxTokens = MaxOutputTokens, Messages = messages,
                Temperature = temp,
            },
            _ => new MessageCreateParams
            {
                Model = model, MaxTokens = MaxOutputTokens, Messages = messages,
            },
        };
#pragma warning restore CS0618
    }
}
