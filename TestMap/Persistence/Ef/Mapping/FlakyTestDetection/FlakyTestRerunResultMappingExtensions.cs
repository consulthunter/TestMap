using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef.Entities.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Mapping.FlakyTestDetection;

public static class FlakyTestRerunResultMappingExtensions
{
    public static FlakyTestRerunResultModel ToDomain(this FlakyTestRerunResultEntity entity)
    {
        return new FlakyTestRerunResultModel
        {
            Id = entity.Id,
            RunId = entity.RunId,
            TestExecutionResultId = entity.TestExecutionResultId,
            AttemptNumber = entity.AttemptNumber,
            Outcome = entity.Outcome,
            DurationMs = entity.DurationMs,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt
        };
    }

    public static FlakyTestRerunResultEntity ToEntity(this FlakyTestRerunResultModel model)
    {
        return new FlakyTestRerunResultEntity
        {
            Id = model.Id,
            RunId = model.RunId,
            TestExecutionResultId = model.TestExecutionResultId,
            AttemptNumber = model.AttemptNumber,
            Outcome = model.Outcome,
            DurationMs = model.DurationMs,
            ErrorMessage = model.ErrorMessage,
            CreatedAt = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt
        };
    }
}