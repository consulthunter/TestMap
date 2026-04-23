using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;
using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Services.Testing.Providers.Abstractions;

public abstract class SemanticKernelGenerationProviderBase : IAiGenerationProvider
{
    private const string DefaultSystemPrompt = "You are an expert software tester with experience in csharp.";

    private Kernel? _kernel;
    private IChatCompletionService? _chatCompletionService;
    private ITextGenerationService? _textGenerationService;
    private AiProviderMode _mode;

    public abstract AiProvider Provider { get; }

    public abstract Task CreateAsync(
        IAiProviderConfig providerConfig,
        AiProviderMode mode,
        CancellationToken cancellationToken = default);

    public async Task<string> GenerateAsync(
        string prompt,
        double temperature = 0.0,
        CancellationToken cancellationToken = default)
    {
        EnsureCreated();

        return _mode == AiProviderMode.Chat
            ? await GenerateChatAsync(prompt, temperature, cancellationToken)
            : await GenerateInferenceAsync(prompt, temperature, cancellationToken);
    }

    protected void Initialize(
        AiProviderMode mode,
        IChatCompletionService? chatCompletionService,
        ITextGenerationService? textGenerationService,
        Kernel? kernel = null)
    {
        _kernel = kernel ?? Kernel.CreateBuilder().Build();
        _mode = mode;
        _chatCompletionService = chatCompletionService;
        _textGenerationService = textGenerationService;
    }

    protected void Initialize(Kernel kernel, AiProviderMode mode)
    {
        Initialize(
            mode,
            kernel.Services.GetService<IChatCompletionService>(),
            kernel.Services.GetService<ITextGenerationService>(),
            kernel);
    }

    protected Kernel BuildKernel(Action<IKernelBuilder> configure)
    {
        var builder = Kernel.CreateBuilder();
        configure(builder);
        return builder.Build();
    }

    protected abstract PromptExecutionSettings CreateExecutionSettings(double temperature);

    private async Task<string> GenerateChatAsync(
        string prompt,
        double temperature,
        CancellationToken cancellationToken)
    {
        if (_chatCompletionService == null || _kernel == null)
        {
            throw new InvalidOperationException("Chat completion service was not initialized.");
        }

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(DefaultSystemPrompt);
        chatHistory.AddUserMessage(prompt);

        var results = await _chatCompletionService.GetChatMessageContentsAsync(
            chatHistory,
            CreateExecutionSettings(temperature),
            _kernel,
            cancellationToken);

        return results.FirstOrDefault()?.Content ?? string.Empty;
    }

    private async Task<string> GenerateInferenceAsync(
        string prompt,
        double temperature,
        CancellationToken cancellationToken)
    {
        if (_kernel == null)
        {
            throw new InvalidOperationException("Text generation service was not initialized.");
        }

        if (_textGenerationService != null)
        {
            var results = await _textGenerationService.GetTextContentsAsync(
                prompt,
                CreateExecutionSettings(temperature),
                _kernel,
                cancellationToken);

            return results.FirstOrDefault()?.Text ?? string.Empty;
        }

        if (_chatCompletionService != null)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var results = await _chatCompletionService.GetChatMessageContentsAsync(
                chatHistory,
                CreateExecutionSettings(temperature),
                _kernel,
                cancellationToken);

            return results.FirstOrDefault()?.Content ?? string.Empty;
        }

        throw new InvalidOperationException("Text generation service is not available for this provider.");
    }

    private void EnsureCreated()
    {
        if (_kernel == null)
        {
            throw new InvalidOperationException("Provider was not initialized.");
        }

        if (_mode == AiProviderMode.Chat && _chatCompletionService == null)
        {
            throw new InvalidOperationException("Chat completion service is not available for this provider.");
        }

        if (_mode == AiProviderMode.Inference && _textGenerationService == null && _chatCompletionService == null)
        {
            throw new InvalidOperationException("Text generation service is not available for this provider.");
        }
    }
}
