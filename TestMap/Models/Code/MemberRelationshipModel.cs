namespace TestMap.Models.Code;

public class MemberRelationshipModel(
    int sourceId = 0,
    int targetId = 0,
    string relationshipType = "",
    int id = 0)
{
    public int Id { get; set; } = id;
    public int SourceId { get; set; } = sourceId;
    public int TargetId { get; set; } = targetId;
    public string RelationshipType { get; set; } = relationshipType;
    public string ContentHash => Utilities.Utilities.ComputeSha256($"{SourceId}:{TargetId}:{RelationshipType}");
}
