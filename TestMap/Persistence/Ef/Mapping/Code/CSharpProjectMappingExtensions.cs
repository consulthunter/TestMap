using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Mapping.Code;

public static class CSharpProjectMappingExtensions
{
    public static CSharpProjectEntity ToEntity(this CSharpProjectModel model, int solutionId)
    {
        return new CSharpProjectEntity
        {
            SolutionId = solutionId,
            FilePath = model.FilePath,
            BuildTargets = model.BuildTargets,
            DefaultBuildTarget = model.DefaultBuildTarget,
            BuildMetadata = model.BuildMetadata,
            ContentHash = model.ContentHash
        };
    }

    public static CSharpProjectModel ToDomain(this CSharpProjectEntity entity)
    {
        return new CSharpProjectModel(
            new List<string>(),
            new List<string>(),
            entity.BuildTargets,
            entity.BuildMetadata,
            entity.Id,
            entity.SolutionId,
            entity.FilePath,
            entity.DefaultBuildTarget
        );
    }
}