using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TestMap.Models.Configuration.AiProviders;

namespace TestMap.Models.Configuration.Testing.Generation;

public sealed class GenerationProfile
{
    public TestGenerationObjective Objective { get; set; } = TestGenerationObjective.TestSuiteExpansion;
    public AiProvider Provider { get; set; } = AiProvider.OpenAi;
    public string ModelName { get; set; } = string.Empty;
    public TestGenerationApproach Approach { get; set; } = TestGenerationApproach.MetricsDriven;
    public MetricsDrivenPath? MetricsPath { get; set; } = MetricsDrivenPath.CoverageAndMutation;
    public TestActionExecutorMode Executor { get; set; } = TestActionExecutorMode.BasicExtension;
    public GenerationBudgetMode BudgetMode { get; set; } = GenerationBudgetMode.PassAt1RepairAt5;
    public GenerationContextMode ContextMode { get; set; } = GenerationContextMode.ChainedHistory;
    public GenerationStepConfig Steps { get; set; } = new();
    public double Temperature { get; set; }
    public int StepErrorRetries { get; set; }
    public int StepRetryDelayMs { get; set; } = 1000;

    public string ToStableJson()
    {
        return JsonSerializer.Serialize(
            this,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                Converters = { new JsonStringEnumConverter() }
            });
    }

    public string ToStableHash()
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ToStableJson()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
