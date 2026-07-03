namespace TestMap.Persistence.Ef.Entities.Code;

public class CSharpSolutionEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}