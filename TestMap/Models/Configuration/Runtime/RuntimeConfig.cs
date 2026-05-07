namespace TestMap.Models.Configuration.Runtime;

public class RuntimeConfig
{
    public ProjectConfig Project { get; set; } = new();
    public FilePathConfig FilePaths { get; set; } = new();
    public DockerConfig Docker { get; set; } = new();
    public Dictionary<string, List<string>>? Frameworks { get; set; }
    public int MaxConcurrency { get; set; }
    public string RunDateFormat { get; set; } = "yyyy-MM-dd";
}
