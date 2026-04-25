using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Mapping.Code;

public static class MemberRelationshipMappingExtensions
{
    public static MemberRelationshipEntity ToEntity(this MemberRelationshipModel model)
    {
        return new MemberRelationshipEntity
        {
            SourceId = model.SourceId,
            TargetId = model.TargetId,
            RelationshipType = model.RelationshipType
        };
    }

    public static MemberRelationshipModel ToDomain(this MemberRelationshipEntity entity)
    {
        return new MemberRelationshipModel(
            entity.SourceId,
            entity.TargetId,
            entity.RelationshipType,
            entity.Id);
    }
}