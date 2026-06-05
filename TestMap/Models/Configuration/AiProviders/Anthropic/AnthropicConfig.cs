namespace TestMap.Models.Configuration.AiProviders.Anthropic;

public class AnthropicConfig : IAiProviderConfig
{
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public AiProvider Provider { get; set; } = AiProvider.Anthropic;
}
