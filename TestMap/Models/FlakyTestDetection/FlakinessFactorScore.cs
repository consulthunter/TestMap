using TestMap.Models.Configuration.Testing.FlakyDetection;

namespace TestMap.Models.FlakyTestDetection;

public sealed record FlakinessFactorScore(
    FlakinessFactorKind Factor,
    double Score,
    string Evidence);