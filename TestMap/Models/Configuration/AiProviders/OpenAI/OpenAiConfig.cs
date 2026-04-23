

namespace TestMap.Models.Configuration.AiProviders.OpenAI;

public class OpenAiConfig : IAiProviderConfig
{
    public string Model { get; set; } = "";
    public string OrgId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public AiProvider Provider { get; set; } = AiProvider.OpenAi;   
}