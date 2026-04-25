using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.Configuration.Testing.MetadataEnrichment;

namespace TestMap.Models.Configuration.Testing;

public class TestingConfig
{
    public GenerationConfig GenerationConfig { get; set; } = new();
    public FlakyTestDetectionConfig FlakyTestDetectionConfig { get; set; } = new();
    public MetadataEnrichmentConfig MetadataEnrichmentConfig { get; set; } = new();
    public List<IFrameworkConfig> TestingFrameworks { get; set; } = new();
}