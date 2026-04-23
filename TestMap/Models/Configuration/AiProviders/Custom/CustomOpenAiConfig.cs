

namespace TestMap.Models.Configuration.AiProviders.Custom;

public class CustomOpenAiConfig : IAiProviderConfig
{
    public string Model { get; set; } = "";
    public string OrgId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    
    public AiProvider Provider { get; set; } = AiProvider.CustomOpenAi;
}