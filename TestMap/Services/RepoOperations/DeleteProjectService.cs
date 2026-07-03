/*
 * consulthunter
 * 2024-11-07
 * Removes the project from
 * the Temp directory
 * DeleteProjectService.cs
 */


using TestMap.App;

namespace TestMap.Services.RepoOperations;

public class DeleteProjectService(ProjectContext context) : IDeleteProjectService
{
    /// <summary>
    ///     Removes the project directory from the file system
    /// </summary>
    public Task DeleteProjectAsync()
    {
        if (ShouldKeepProjectFiles())
        {
            context.Project.Logger?.Information(
                "Skipping repository deletion because KeepProjectFiles is enabled.");
            return Task.CompletedTask;
        }

        if (!Directory.Exists(context.Project.DirectoryPath))
        {
            context.Project.Logger?.Warning($"Directory {context.Project.DirectoryPath} does not exist.");
            return Task.CompletedTask;
        }

        try
        {
            context.Project.Logger?.Information($"Deleting repository: {context.Project.GitHubUrl}");

            // Remove ReadOnly attribute recursively before deleting
            RemoveReadOnlyAttributes(context.Project.DirectoryPath);

            // Recursively delete the directory
            Directory.Delete(context.Project.DirectoryPath, true);

            context.Project.Logger?.Information($"Successfully deleted repository: {context.Project.GitHubUrl}");
        }
        catch (Exception ex)
        {
            context.Project.Logger?.Error($"Failed to delete repository: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private bool ShouldKeepProjectFiles()
    {
        return context.Project.Config.RuntimeConfig.Project.KeepProjectFiles;
    }

    /// <summary>
    /// Recursively removes ReadOnly attribute from all files in the given directory.
    /// </summary>
    private void RemoveReadOnlyAttributes(string directoryPath)
    {
        var directoryInfo = new DirectoryInfo(directoryPath);

        foreach (var file in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
            if (file.IsReadOnly)
            {
                file.IsReadOnly = false;
                context.Project.Logger?.Information($"Removed ReadOnly attribute from file: {file.FullName}");
            }
    }
}
