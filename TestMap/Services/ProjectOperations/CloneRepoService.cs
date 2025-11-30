/*
 * consulthunter
 * 2024-11-07
 * Clones the project from the remote
 * using the URL and LibGit2Sharp
 * CloneRepoService.cs
 */

using LibGit2Sharp;
using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class CloneRepoService(ProjectModel projectModel) : ICloneRepoService
{
    /// <summary>
    ///     Entry point into the service
    /// </summary>
    public virtual async Task CloneRepoAsync()
    {
        await Clone();
    }

    /// <summary>
    ///     Clones a repository using LibGit2Sharp
    ///     if it's not already cloned.
    /// </summary>
    /// <returns></returns>
    private Task Clone()
    {
        try
        {
            if (Repository.IsValid(projectModel.DirectoryPath))
            {
                // Ensure no file handles are left open
                using (var repo = new Repository(projectModel.DirectoryPath))
                {
                }

                projectModel.Logger?.Information(
                    $"Repository already exists at {projectModel.DirectoryPath}, skipping clone.");
                return Task.CompletedTask;
            }

            var parentDir = Directory.GetParent(projectModel.DirectoryPath);
            if (parentDir is { Exists: true })
            {
                projectModel.Logger?.Information($"Cloning repository: {projectModel.GitHubUrl}");

                Repository.Clone(projectModel.GitHubUrl, projectModel.DirectoryPath);

                // Immediately dispose to release file locks
                using (var repo = new Repository(projectModel.DirectoryPath))
                {
                }

                projectModel.Logger?.Information($"Finished cloning repository: {projectModel.GitHubUrl}");
            }
            else
            {
                projectModel.Logger?.Error($"Parent directory {parentDir?.FullName} does not exist.");
            }
        }
        catch (Exception ex)
        {
            projectModel.Logger?.Error($"Failed to clone repository: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}