namespace TestMap.Models.Configuration;

public class GenerationConfig
{
    public string Provider { get; set; } = "heuristic";
    public Dictionary<string, object> Parameters { get; set; } = new();
}