using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef.Entities.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Mapping.FlakyTestDetection;

public static class TestExecutionResultMappingExtensions
{
    public static TestExecutionResultModel ToDomain(this TestExecutionResultEntity entity)
    {
        return new TestExecutionResultModel
        {
            Id = entity.Id,
            RunId = entity.RunId,
            SolutionPath = entity.SolutionPath,
            ProjectPath = entity.ProjectPath,
            TestMemberId = entity.TestMemberId,
            TestName = entity.TestName,
            FilePath = entity.FilePath,
            TargetFramework = entity.TargetFramework,
            ExecutionContext = entity.ExecutionContext,
            Outcome = entity.Outcome,
            DurationMs = entity.DurationMs,
            ErrorMessage = entity.ErrorMessage,
            ErrorStackTrace = entity.ErrorStackTrace,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            CreatedAt = entity.CreatedAt
        };
    }

    public static TestExecutionResultEntity ToEntity(this TestExecutionResultModel model)
    {
        return new TestExecutionResultEntity
        {
            Id = model.Id,
            RunId = model.RunId,
            SolutionPath = model.SolutionPath,
            ProjectPath = model.ProjectPath,
            TestMemberId = model.TestMemberId,
            TestName = model.TestName,
            FilePath = model.FilePath,
            TargetFramework = model.TargetFramework,
            ExecutionContext = model.ExecutionContext,
            Outcome = model.Outcome,
            DurationMs = model.DurationMs,
            ErrorMessage = model.ErrorMessage,
            ErrorStackTrace = model.ErrorStackTrace,
            StartedAt = model.StartedAt,
            CompletedAt = model.CompletedAt,
            CreatedAt = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt
        };
    }
}
