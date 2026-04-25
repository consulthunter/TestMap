using TestMap.Models.Configuration.AiProviders;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Models.Configuration.Testing.Generation;

public class GenerationConfig
{
    public AiProvider Provider { get; set; } = AiProvider.OpenAi;
    public AiProviderMode Mode { get; set; } = AiProviderMode.Chat;
    public TestGenerationApproach Strategy { get; set; } = TestGenerationApproach.ActionAware;
    public TestActionExecutorMode Executor { get; set; } = TestActionExecutorMode.ActionAware;
    public int MaxRetries { get; set; } = 1;
    public bool EnableHistoryChaining { get; set; }
    public TargetSelectionConfig TargetSelection { get; set; } = new();
    public TestBootstrapConfig Bootstrap { get; set; } = new();
}