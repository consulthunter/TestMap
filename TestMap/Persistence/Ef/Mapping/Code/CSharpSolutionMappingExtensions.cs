using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Mapping.Code;

public static class CSharpSolutionMappingExtensions
{
    public static CSharpSolutionEntity ToEntity(this CSharpSolutionModel model, int projectId)
    {
        return new CSharpSolutionEntity
        {
            ProjectId = projectId,
            FilePath = model.FilePath,
            ContentHash = model.ContentHash
        };
    }

    public static CSharpSolutionModel ToDomain(this CSharpSolutionEntity entity)
    {
        return new CSharpSolutionModel(
            new List<string>(),
            entity.Id,
            entity.ProjectId,
            entity.FilePath);
    }
}