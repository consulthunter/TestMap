using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef.Entities.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Mapping.FlakyTestDetection;

public static class FlakyTestScoreMappingExtensions
{
    public static FlakyTestScoreModel ToDomain(this FlakyTestScoreEntity entity)
    {
        return new FlakyTestScoreModel
        {
            Id = entity.Id,
            RunId = entity.RunId,
            TestMemberId = entity.TestMemberId,
            TestName = entity.TestName,
            FilePath = entity.FilePath,
            FlakinessScore = entity.FlakinessScore,
            Classification = entity.Classification,
            FactorScores = entity.FactorScores,
            Weights = entity.Weights,
            Evidence = entity.Evidence,
            CreatedAt = entity.CreatedAt
        };
    }

    public static FlakyTestScoreEntity ToEntity(this FlakyTestScoreModel model)
    {
        return new FlakyTestScoreEntity
        {
            Id = model.Id,
            RunId = model.RunId,
            TestMemberId = model.TestMemberId,
            TestName = model.TestName,
            FilePath = model.FilePath,
            FlakinessScore = model.FlakinessScore,
            Classification = model.Classification,
            FactorScores = model.FactorScores,
            Weights = model.Weights,
            Evidence = model.Evidence,
            CreatedAt = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt
        };
    }
}