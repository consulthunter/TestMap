namespace TestMap.Models.Configuration.Testing.FlakyDetection;

public enum FlakinessFactorKind
{
    OutcomeVariance,
    RerunInstability,
    DurationVariance,
    FailureSignature,
    EnvironmentSignal
}
