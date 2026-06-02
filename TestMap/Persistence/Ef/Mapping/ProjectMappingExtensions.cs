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
        return new ProjectModel(
            gitHubUrl: project.WebUrl ?? string.Empty,
            owner: project.Owner,
            repoName: project.RepoName,
            directoryPath: project.DirectoryPath,
            databasePath: project.DatabasePath)
        {
            DbId = project.Id,
            Branch = project.Branch,
            LastAnalyzedCommit = project.LastAnalyzedCommit
        };
    }
}