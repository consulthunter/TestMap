namespace TestMap.Models.Configuration.Testing.Generation;

public class TargetSelectionConfig
{
    public TargetSelectionStrategy Strategy { get; set; } = TargetSelectionStrategy.Existing;
    public int CandidateLimit { get; set; } = 20;
    public TestContextMappingMode ContextMappingMode { get; set; } =
        TestContextMappingMode.HeuristicWithGroundedPreference;
    public RiskWeightsConfig RiskWeights { get; set; } = new();
    public MetricDrivenImprovementConfig MetricDrivenImprovement { get; set; } = new();
    public bool FailOnMissingRiskInputs { get; set; } = true;
}
