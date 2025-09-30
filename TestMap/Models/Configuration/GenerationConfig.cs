namespace TestMap.Models.Configuration;

public class GenerationConfig
{
    public string Provider { get; set; } = "openai";
    public string OrgId { get; set; } = "";
    public string Model { get; set; } = "gpt-3.5-turbo";
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public int MaxRetries { get; set; } = 1;
}