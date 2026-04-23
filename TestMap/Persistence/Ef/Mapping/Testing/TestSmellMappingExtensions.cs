using TestMap.Models.Results;
using TestMap.Persistence.Ef.Entities.Testing;

namespace TestMap.Persistence.Ef.Mappings;

public static class TestSmellMappingExtensions
{
    public static TestSmellModel ToDomain(this TestSmellEntity entity)
    {
        return new TestSmellModel
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            MemberId = entity.MemberId,
            ObjectId = entity.ObjectId,
            SmellId = entity.SmellId,
            SmellName = entity.SmellName,
            Message = entity.Message,
            FilePath = entity.FilePath,
            Line = entity.Line,
            Column = entity.Column,
            ContainingTypeName = entity.ContainingTypeName,
            TestMethodName = entity.TestMethodName,
            AnalyzedAtUtc = entity.AnalyzedAtUtc,
            CreatedAt = entity.CreatedAt
        };
    }

    public static TestSmellEntity ToEntity(this TestSmellModel model)
    {
        return new TestSmellEntity
        {
            ProjectId = model.ProjectId,
            MemberId = model.MemberId,
            ObjectId = model.ObjectId,
            SmellId = model.SmellId,
            SmellName = model.SmellName,
            Message = model.Message,
            FilePath = model.FilePath,
            Line = model.Line,
            Column = model.Column,
            ContainingTypeName = model.ContainingTypeName,
            TestMethodName = model.TestMethodName,
            AnalyzedAtUtc = model.AnalyzedAtUtc,
            CreatedAt = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt
        };
    }
}
