using TestMap.Models;
using TestMap.Persistence.Ef.Entities;

namespace TestMap.Persistence.Ef.Mapping;

public static class ProjectMappingExtensions
{
    public static ProjectEntity ToEntity(this ProjectModel project)
    {
        return new ProjectEntity
        {
            Owner = project.Owner,
            RepoName = project.RepoName,
            WebUrl = project.GitHubUrl,
            Branch = project.Branch,
            LastAnalyzedCommit = project.LastAnalyzedCommit,
            DatabasePath = project.DatabasePath,
            ContentHash = project.ContentHash,
            DirectoryPath = project.DirectoryPath
        };
    }

    public static ProjectModel ToDomain(this ProjectEntity project)
    {
        return new ProjectModel
        {
            GitHubUrl = project.WebUrl ?? string.Empty
        };
    }
}