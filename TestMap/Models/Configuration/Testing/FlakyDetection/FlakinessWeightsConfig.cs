namespace TestMap.Models.Configuration.Testing.FlakyDetection;

public class FlakinessWeightsConfig
{
    public double OutcomeVariance { get; set; } = 0.35;
    public double RerunInstability { get; set; } = 0.25;
    public double DurationVariance { get; set; } = 0.15;
    public double FailureSignature { get; set; } = 0.15;
    public double EnvironmentSignal { get; set; } = 0.10;

    public IReadOnlyDictionary<FlakinessFactorKind, double> ToDictionary()
    {
        return new Dictionary<FlakinessFactorKind, double>
        {
            [FlakinessFactorKind.OutcomeVariance] = OutcomeVariance,
            [FlakinessFactorKind.RerunInstability] = RerunInstability,
            [FlakinessFactorKind.DurationVariance] = DurationVariance,
            [FlakinessFactorKind.FailureSignature] = FailureSignature,
            [FlakinessFactorKind.EnvironmentSignal] = EnvironmentSignal
        };
    }
}
