using TestMap.Models;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Entities.Testing;

namespace TestMap.Persistence.Ef.Mappings;

public static class TestRunMappingExtensions
{
    public static TestRunModel ToDomain(this TestRunEntity entity)
    {
        return new TestRunModel
        {
            RunId = entity.RunId,
            RunDate = entity.RunDate,
            Success = entity.Success,
            Coverage = entity.Coverage,
            MutationScore = entity.MutationScore,
            LogPath = entity.LogPath,
            FailureAnalysis = entity.FailureAnalysis
        };
    }

    public static TestRunEntity ToEntity(this TestRunModel model, int projectId)
    {
        return new TestRunEntity
        {
            ProjectId = projectId,
            RunId = model.RunId,
            RunDate = model.RunDate,
            Success = model.Success,
            Coverage = model.Coverage,
            MutationScore = model.MutationScore,
            LogPath = model.LogPath,
            FailureAnalysis = model.FailureAnalysis,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static TestRunEntity ToEntity(this ProjectModel projectModel, bool success, int coverage, string logPath)
    {
        return new TestRunEntity
        {
            ProjectId = projectModel.DbId,
            RunId = projectModel.ProjectId ?? "",
            RunDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Success = success,
            Coverage = coverage,
            MutationScore = null,
            LogPath = logPath,
            FailureAnalysis = null,
            CreatedAt = DateTime.UtcNow
        };
    }
}