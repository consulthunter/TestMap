namespace TestMap.Models.Configuration.Testing.Generation;

public class TestBootstrapConfig
{
    public bool Enabled { get; set; } = true;
    public string DefaultFramework { get; set; } = "xUnit";
    public string TestProjectSuffix { get; set; } = ".Tests";
    public bool AddCoverletCollector { get; set; } = true;
    public int InitialCandidateLimit { get; set; } = 10;
}