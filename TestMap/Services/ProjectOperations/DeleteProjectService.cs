/*
 * consulthunter
 * 2024-11-07
 * Removes the project from
 * the Temp directory
 * DeleteProjectService.cs
 */

using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class DeleteProjectService(ProjectModel projectModel) : IDeleteProjectService
{
    /// <summary>
    ///     Removes the project directory from the file system
    /// </summary>
    public Task DeleteProjectAsync()
    {
        if (!Directory.Exists(projectModel.DirectoryPath))
        {
            projectModel.Logger?.Warning($"Directory {projectModel.DirectoryPath} does not exist.");
            return Task.CompletedTask;
        }

        try
        {
            projectModel.Logger?.Information($"Deleting repository: {projectModel.GitHubUrl}");

            // Remove ReadOnly attribute recursively before deleting
            RemoveReadOnlyAttributes(projectModel.DirectoryPath);

            // Recursively delete the directory
            Directory.Delete(projectModel.DirectoryPath, recursive: true);

            projectModel.Logger?.Information($"Successfully deleted repository: {projectModel.GitHubUrl}");
        }
        catch (Exception ex)
        {
            projectModel.Logger?.Error($"Failed to delete repository: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Recursively removes ReadOnly attribute from all files in the given directory.
    /// </summary>
    private void RemoveReadOnlyAttributes(string directoryPath)
    {
        var directoryInfo = new DirectoryInfo(directoryPath);

        foreach (var file in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
        {
            if (file.IsReadOnly)
            {
                file.IsReadOnly = false;
                projectModel.Logger?.Information($"Removed ReadOnly attribute from file: {file.FullName}");
            }
        }
    }

}