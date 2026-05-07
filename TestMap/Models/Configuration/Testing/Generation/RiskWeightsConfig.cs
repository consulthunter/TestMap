namespace TestMap.Models.Configuration.Testing.Generation;

public class RiskWeightsConfig
{
    public double CoverageGap { get; set; } = 0.30;
    public double MutationSurvival { get; set; } = 0.25;
    public double Complexity { get; set; } = 0.15;
    public double CallGraph { get; set; } = 0.10;
    public double Churn { get; set; } = 0.10;
    public double TestGap { get; set; } = 0.10;

    public IReadOnlyDictionary<RiskFactorKind, double> ToDictionary()
    {
        return new Dictionary<RiskFactorKind, double>
        {
            [RiskFactorKind.CoverageGap] = CoverageGap,
            [RiskFactorKind.MutationSurvival] = MutationSurvival,
            [RiskFactorKind.Complexity] = Complexity,
            [RiskFactorKind.CallGraph] = CallGraph,
            [RiskFactorKind.Churn] = Churn,
            [RiskFactorKind.TestGap] = TestGap
        };
    }
}