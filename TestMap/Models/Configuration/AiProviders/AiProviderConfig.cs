using TestMap.Models.Configuration.AiProviders.Amazon;
using TestMap.Models.Configuration.AiProviders.Custom;
using TestMap.Models.Configuration.AiProviders.Google;
using TestMap.Models.Configuration.AiProviders.Ollama;
using TestMap.Models.Configuration.AiProviders.OpenAI;

namespace TestMap.Models.Configuration.AiProviders;

public class AiProviderConfig
{
    public OpenAiConfig OpenAi { get; set; } = new() { Provider = AiProvider.OpenAi };
    public AmazonConfig Amazon { get; set; } = new() { Provider = AiProvider.Amazon };
    public GoogleGeminiConfig GoogleGemini { get; set; } = new() { Provider = AiProvider.GoogleGemini };
    public GoogleCloudConfig GoogleCloud { get; set; } = new() { Provider = AiProvider.GoogleCloud };
    public CustomOpenAiConfig CustomOpenAi { get; set; } = new() { Provider = AiProvider.CustomOpenAi };
    public OllamaConfig Ollama { get; set; } = new() { Provider = AiProvider.Ollama };

    public IReadOnlyList<IAiProviderConfig> ProviderConfigs =>
    [
        OpenAi,
        Amazon,
        GoogleGemini,
        GoogleCloud,
        CustomOpenAi,
        Ollama
    ];

    public IAiProviderConfig? GetProviderConfig(AiProvider provider)
    {
        return provider switch
        {
            AiProvider.OpenAi => OpenAi,
            AiProvider.Amazon => Amazon,
            AiProvider.GoogleGemini => GoogleGemini,
            AiProvider.GoogleCloud => GoogleCloud,
            AiProvider.CustomOpenAi => CustomOpenAi,
            AiProvider.Ollama => Ollama,
            _ => null
        };
    }
}
