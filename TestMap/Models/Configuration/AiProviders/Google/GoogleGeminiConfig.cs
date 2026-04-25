namespace TestMap.Models.Configuration.AiProviders.Google;

public class GoogleGeminiConfig : IAiProviderConfig
{
    public string Model { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Location { get; set; } = "";
    public string TokenPath { get; set; } = "";
    public AiProvider Provider { get; set; } = AiProvider.GoogleGemini;
}