namespace TestMap.Persistence.Ef.Entities.Code;

public class ObjectRelationshipEntity
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
}
