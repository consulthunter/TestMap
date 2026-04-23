namespace TestMap.Persistence.Ef.Entities.Testing;

public class TestSmellEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? MemberId { get; set; }
    public int? ObjectId { get; set; }
    public string SmellId { get; set; } = string.Empty;
    public string SmellName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int? Line { get; set; }
    public int? Column { get; set; }
    public string ContainingTypeName { get; set; } = string.Empty;
    public string TestMethodName { get; set; } = string.Empty;
    public DateTimeOffset AnalyzedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
}
