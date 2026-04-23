using TestMap.Models.Configuration.AiProviders;
using TestMap.Services.Testing.Providers.Abstractions;

namespace TestMap.Models.Configuration.Testing.MetadataEnrichment;

public class MetadataEnrichmentConfig
{
    public bool Enabled { get; set; } = true;
    public bool UseModel { get; set; } = true;
    public AiProvider Provider { get; set; } = AiProvider.OpenAi;
    public AiProviderMode Mode { get; set; } = AiProviderMode.Chat;
    public double Temperature { get; set; } = 0.0;
    public int MaxCategories { get; set; } = 3;
    public string PromptVersion { get; set; } = "test-metadata-v1";
}
