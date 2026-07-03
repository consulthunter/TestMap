namespace TestMap.Models.Configuration.Testing.Generation;

public class MetricDrivenGuardrailsConfig
{
    public double MaxFlakinessScore { get; set; } = 40;
    public int MaxTestSmellIncrease { get; set; } = 0;
    public bool RequireMeaningfulAssertions { get; set; } = true;
    public bool AvoidImplementationDetailAssertions { get; set; } = true;
    public bool ExcludeFailedGuardrails { get; set; } = true;
}