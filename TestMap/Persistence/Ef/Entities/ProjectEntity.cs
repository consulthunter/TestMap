using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities;

public class ProjectEntity
{
    public int Id { get; set; }
    [MaxLength(255)] public string Owner { get; set; } = string.Empty;
    [MaxLength(255)] public string RepoName { get; set; } = string.Empty;
    [MaxLength(2048)] public string DirectoryPath { get; set; } = string.Empty;
    [MaxLength(255)] public string? WebUrl { get; set; }
    [MaxLength(2048)] public string? DatabasePath { get; set; }
    [MaxLength(255)] public string? Branch { get; set; }
    [MaxLength(255)] public string? LastAnalyzedCommit { get; set; }
    [MaxLength(255)] public string? ContentHash { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}