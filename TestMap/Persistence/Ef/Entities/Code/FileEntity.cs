namespace TestMap.Persistence.Ef.Entities.Code;

public class FileEntity
{
    public int Id { get; set; }
    public int CSharpProjectId { get; set; }
    public List<string> UsingStatements { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}