using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Mapping.Code;

public static class ObjectRelationshipMappingExtensions
{
    public static ObjectRelationshipEntity ToEntity(this ObjectRelationshipModel model)
    {
        return new ObjectRelationshipEntity
        {
            SourceId = model.SourceId,
            TargetId = model.TargetId,
            RelationshipType = model.RelationshipType
        };
    }

    public static ObjectRelationshipModel ToDomain(this ObjectRelationshipEntity entity)
    {
        return new ObjectRelationshipModel(
            entity.SourceId,
            entity.TargetId,
            entity.RelationshipType,
            entity.Id);
    }
}