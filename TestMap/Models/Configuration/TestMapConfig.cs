using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Runtime;
using TestMap.Models.Configuration.Testing;
using TestMap.Models.Experiment;

namespace TestMap.Models.Configuration;

public class TestMapConfig
{
    public RuntimeConfig RuntimeConfig { get; set; } = new();
    public TestingConfig TestingConfig { get; set; } = new();
    public AiProviderConfig AiProviderConfig { get; set; } = new();
    public ExperimentConfiguration? ExperimentConfig { get; set; }
}