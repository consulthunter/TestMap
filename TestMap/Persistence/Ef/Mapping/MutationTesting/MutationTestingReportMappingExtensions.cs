using TestMap.Models.Results;
using TestMap.Persistence.Ef.Entities.MutationTesting;

namespace TestMap.Persistence.Ef.Mappings;

public static class MutationTestingReportMappingExtensions
{
    public static StrykerMutationResults ToDomain(this MutationTestingReportEntity entity)
    {
        return new StrykerMutationResults
        {
            schemaVersion = entity.SchemaVersion,
            projectRoot = entity.ProjectRoot,
            files = entity.Files,
            testFiles = entity.TestFiles,
            thresholds = entity.Thresholds
        };
    }

    public static MutationTestingReportEntity ToEntity(this StrykerMutationResults model, int projectId, double mutationScore)
    {
        return new MutationTestingReportEntity
        {
            ProjectId = projectId,
            SchemaVersion = model.schemaVersion,
            ProjectRoot = model.projectRoot,
            MutationScore = mutationScore,
            Files = model.files,
            TestFiles = model.testFiles,
            Thresholds = model.thresholds,
            CreatedAt = DateTime.UtcNow
        };
    }
}
