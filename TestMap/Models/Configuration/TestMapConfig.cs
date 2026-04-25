using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Runtime;
using TestMap.Models.Configuration.Testing;

namespace TestMap.Models.Configuration;

public class TestMapConfig
{
    public RuntimeConfig RuntimeConfig { get; set; } = new();
    public TestingConfig TestingConfig { get; set; } = new();
    public AiProviderConfig AiProviderConfig { get; set; } = new();
    public ExperimentConfig ExperimentConfig { get; set; } = new();
}
