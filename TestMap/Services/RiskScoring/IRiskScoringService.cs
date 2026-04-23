using TestMap.Models.RiskScoring;

namespace TestMap.Services.RiskScoring;

public interface IRiskScoringService
{
    RiskScoringValidationResult ValidateWeights(RiskScoringRequest request);
    Task<IReadOnlyList<MethodRiskScore>> ScoreAsync(
        RiskScoringRequest request,
        CancellationToken cancellationToken = default);
}
