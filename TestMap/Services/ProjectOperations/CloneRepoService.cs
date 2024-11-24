/*
 * consulthunter
 * 2024-11-07
 * Clones the project from the remote
 * using the URL and LibGit2Sharp
 * CloneRepoService.cs
 */

using TestMap.Models;
using LibGit2Sharp;
namespace TestMap.Services.ProjectOperations;

public class CloneRepoService(ProjectModel projectModel) : ICloneRepoService
{
    /// <summary>
    /// Entry point into the service
    /// </summary>
    public virtual async Task CloneRepoAsync()
    {
        await Clone();
    }

    /// <summary>
    /// Clones a repository using LibGit2Sharp
    /// and the URL
    /// </summary>
    /// <returns></returns>
    private Task Clone()
    {
        // Clone repository
        if (Directory.GetParent(projectModel.DirectoryPath).Exists)
            try
            {
                projectModel.Logger.Information($"Cloning repository: {projectModel.GitHubUrl}");
                Repository.Clone(projectModel.GitHubUrl, projectModel.DirectoryPath);
                projectModel.Logger.Information($"Finished cloning repository: {projectModel.GitHubUrl}");
            }
            catch (Exception ex)
            {
                projectModel.Logger.Error($"Failed to clone repository: {ex.Message}");
            }
        else
            projectModel.Logger.Error($"Directory {projectModel.DirectoryPath} does not exist.");

        return Task.CompletedTask;
    }
}