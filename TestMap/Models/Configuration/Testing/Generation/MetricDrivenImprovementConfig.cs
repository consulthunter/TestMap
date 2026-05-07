namespace TestMap.Models.Configuration.Testing.Generation;

public class MetricDrivenImprovementConfig
{
    public MetricDrivenMetric Metric { get; set; } = MetricDrivenMetric.MutationScore;
    public MetricOptimizationDirection Direction { get; set; } = MetricOptimizationDirection.Increase;
    public MetricDrivenBudgetConfig Budget { get; set; } = new();
    public MetricDrivenGuardrailsConfig Guardrails { get; set; } = new();
    public MetricDrivenWeightsConfig Weights { get; set; } = new();
}