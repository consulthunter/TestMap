namespace TestMap.Persistence.Ef.Entities;

public class ProjectEntity
{
    public int Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public string? WebUrl { get; set; }
    public string? DatabasePath { get; set; }
    public string? LastAnalyzedCommit { get; set; }
    public string? ContentHash { get; set; }
    public DateTime? CreatedAt { get; set; }
}