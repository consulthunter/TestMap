namespace TestMap.Models.Configuration;

public class GenerationConfig
{
    public string Provider { get; set; } = "openai";
    public string Model { get; set; } = "gpt-3.5-turbo";
    public int MaxRetries { get; set; } = 1;
}