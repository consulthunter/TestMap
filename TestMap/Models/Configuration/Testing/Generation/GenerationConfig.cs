using TestMap.Models.Configuration.AiProviders;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.Models.Configuration.Testing.Generation;

public class GenerationConfig
{
    public TestGenerationObjective Objective { get; set; } = TestGenerationObjective.TestSuiteExpansion;
    public AiProvider Provider { get; set; } = AiProvider.OpenAi;
    public AiProviderMode Mode { get; set; } = AiProviderMode.Chat;
    public TestGenerationApproach Strategy { get; set; } = TestGenerationApproach.MetricsDriven;
    public MetricsDrivenPath MetricsPath { get; set; } = MetricsDrivenPath.CoverageAndMutation;
    public TestActionExecutorMode Executor { get; set; } = TestActionExecutorMode.BasicExtension;
    public GenerationBudgetMode BudgetMode { get; set; } = GenerationBudgetMode.PassAt1RepairAt5;
    public double Temperature { get; set; } = 0.0;
    public int StepErrorRetries { get; set; } = 0;
    public int StepRetryDelayMs { get; set; } = 1000;
    public GenerationContextMode ContextMode { get; set; } = GenerationContextMode.ChainedHistory;
    public GenerationStepConfig Steps { get; set; } = new();
    public TargetSelectionConfig TargetSelection { get; set; } = new();
    public TestBootstrapConfig Bootstrap { get; set; } = new();
    public TestAcceptanceConfig Acceptance { get; set; } = new();
}
