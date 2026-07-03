namespace TestMap.Models.Configuration.Runtime;

public class DockerConfig
{
    public string DefaultContext { get; set; } = "";
    public string WindowsContext { get; set; } = "";
    public string WindowsNetwork { get; set; } = "";
    public DockerImageRegistry Images { get; set; } = new();
}

public class DockerImageRegistry
{
    public string ValidationSdkAll { get; set; } = "";
    public Dictionary<string, string> AgentTools { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
