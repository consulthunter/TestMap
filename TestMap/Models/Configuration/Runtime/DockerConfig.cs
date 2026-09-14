namespace TestMap.Models.Configuration.Runtime;

public class DockerConfig
{
    public string DefaultContext { get; set; } = "";
    public string WindowsContext { get; set; } = "";
    public string WindowsNetwork { get; set; } = "";

    /// <summary>
    /// Parallel Stryker test sessions per validation container. Multiplies with
    /// <see cref="RuntimeConfig.MaxConcurrency"/>, since every project runs its own
    /// container. 0 or less passes no flag, so Stryker picks its own default
    /// (half the CPUs the container sees).
    /// </summary>
    public int StrykerConcurrency { get; set; } = 3;

    public DockerImageRegistry Images { get; set; } = new();
}

public class DockerImageRegistry
{
    public string ValidationSdkAll { get; set; } = "";
    public Dictionary<string, string> AgentTools { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
