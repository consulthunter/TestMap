using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Models.RiskScoring;

public sealed record RiskFactorScore(
    RiskFactorKind Factor,
    double Score,
    string Evidence);
