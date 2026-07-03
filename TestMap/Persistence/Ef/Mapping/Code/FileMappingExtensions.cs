using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Mapping.Code;

public static class FileMappingExtensions
{
    public static FileEntity ToEntity(this FileModel model)
    {
        return new FileEntity
        {
            CSharpProjectId = model.AnalysisProjectId,
            FilePath = model.FilePath,
            UsingStatements = model.UsingStatements,
            ContentHash = model.ContentHash
        };
    }

    public static FileModel ToDomain(this FileEntity entity)
    {
        return new FileModel(
            entity.UsingStatements,
            entity.CSharpProjectId,
            entity.FilePath
        )
        {
            Id = entity.Id
        };
    }
}