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
            projectReferences: new List<string>(),
            documentFilePaths: new List<string>(),
            buildTargets: entity.BuildTargets,
            buildMetadata: entity.BuildMetadata,
            id: entity.Id,
            solutionId: entity.SolutionId,
            filePath: entity.FilePath,
            defaultBuildTarget: entity.DefaultBuildTarget
        );
    }
}
