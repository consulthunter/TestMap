namespace TestMap.Persistence.Ef.Entities.Code;

public class MemberRelationshipEntity
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
}