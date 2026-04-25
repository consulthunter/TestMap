namespace TestMap.Models.Configuration.Testing.Generation;

public class MetricDrivenWeightsConfig
{
    public double ExpectedMetricDelta { get; set; } = 0.40;
    public double Confidence { get; set; } = 0.20;
    public double Feasibility { get; set; } = 0.20;
    public double InverseCost { get; set; } = 0.10;
    public double Guardrail { get; set; } = 0.10;

    public IReadOnlyDictionary<string, double> ToDictionary()
    {
        return new Dictionary<string, double>
        {
            [nameof(ExpectedMetricDelta)] = ExpectedMetricDelta,
            [nameof(Confidence)] = Confidence,
            [nameof(Feasibility)] = Feasibility,
            [nameof(InverseCost)] = InverseCost,
            [nameof(Guardrail)] = Guardrail
        };
    }
}