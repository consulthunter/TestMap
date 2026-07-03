namespace TestMap.Models.Configuration.AiProviders.Ollama;

public class OllamaConfig : IAiProviderConfig
{
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "http://localhost:11434/";
    public AiProvider Provider { get; set; } = AiProvider.Ollama;
}