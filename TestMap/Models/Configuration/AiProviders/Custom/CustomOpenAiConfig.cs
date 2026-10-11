namespace TestMap.Models.Configuration.AiProviders.Custom;

public class CustomOpenAiConfig : IAiProviderConfig
{
    public string Model { get; set; } = "";
    public string OrgId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// Per-request timeout. The OpenAI SDK's default of 100 seconds is shorter than a thinking
    /// model's generation time, so requests time out and are retried while the server keeps working.
    /// </summary>
    public int RequestTimeoutMinutes { get; set; } = 15;

    public AiProvider Provider { get; set; } = AiProvider.CustomOpenAi;
}