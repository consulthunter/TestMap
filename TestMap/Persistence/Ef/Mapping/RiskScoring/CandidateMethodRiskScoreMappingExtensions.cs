using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef.Entities.RiskScoring;

namespace TestMap.Persistence.Ef.Mapping.RiskScoring;

public static class CandidateMethodRiskScoreMappingExtensions
{
    public static MethodRiskScore ToDomain(this CandidateMethodRiskScoreEntity entity)
    {
        return new MethodRiskScore
        {
            Id = entity.Id,
            CandidateMethodId = entity.CandidateMethodId,
            MemberId = entity.MemberId,
            RiskScore = entity.RiskScore,
            FactorScores = entity.FactorScores,
            Weights = entity.Weights,
            SelectionReason = entity.SelectionReason,
            CreatedAt = entity.CreatedAt
        };
    }

    public static CandidateMethodRiskScoreEntity ToEntity(this MethodRiskScore model)
    {
        return new CandidateMethodRiskScoreEntity
        {
            Id = model.Id,
            CandidateMethodId = model.CandidateMethodId,
            MemberId = model.MemberId,
            RiskScore = model.RiskScore,
            FactorScores = model.FactorScores,
            Weights = model.Weights,
            SelectionReason = model.SelectionReason,
            CreatedAt = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt
        };
    }
}