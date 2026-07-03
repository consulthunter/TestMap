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
    /// <summary>Max hops through test-support code before the BFS must reach a production member.</summary>
    public int MaxTestCodeDepth { get; set; } = 3;
    /// <summary>Max hops through production code after a test reaches a production entrypoint.</summary>
    public int MaxProductionCallDepth { get; set; } = 3;
    /// <summary>Max production call hops to walk backward when pairing candidates with existing tests.</summary>
    public int MaxIndirectInvocationDepth { get; set; } = 2;
}
