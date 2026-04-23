using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.Configuration.Testing.MetadataEnrichment;

namespace TestMap.Models.Configuration.Testing;

public class TestingConfig
{
    public GenerationConfig GenerationConfig { get; set; } = new GenerationConfig();
    public FlakyTestDetectionConfig FlakyTestDetectionConfig { get; set; } = new FlakyTestDetectionConfig();
    public MetadataEnrichmentConfig MetadataEnrichmentConfig { get; set; } = new MetadataEnrichmentConfig();
    public List<IFrameworkConfig> TestingFrameworks { get; set; } = new List<IFrameworkConfig>();
}
