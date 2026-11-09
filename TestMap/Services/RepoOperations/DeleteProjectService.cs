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

            var workspacePath = ResolveWorkspacePathWithinTempRoot();

            // Remove ReadOnly attribute recursively before deleting
            RemoveReadOnlyAttributes(workspacePath);

            // Recursively delete the directory
            Directory.Delete(workspacePath, true);
            PruneEmptyParents(workspacePath);

            context.Project.Logger?.Information($"Successfully deleted repository: {context.Project.GitHubUrl}");
        }
        catch (Exception ex)
        {
            context.Project.Logger?.Error($"Failed to delete repository: {ex.Message}");
            throw;
        }

        return Task.CompletedTask;
    }

    private bool ShouldKeepProjectFiles()
    {
        return context.Project.Config.RuntimeConfig.Project.KeepProjectFiles;
    }

    private string ResolveWorkspacePathWithinTempRoot()
    {
        var tempRoot = context.Project.TempDirPath;
        if (string.IsNullOrWhiteSpace(tempRoot))
            throw new InvalidOperationException("Repository deletion requires a configured temporary directory root.");

        var fullRoot = Path.GetFullPath(tempRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var workspacePath = Path.GetFullPath(context.Project.DirectoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPrefix = fullRoot + Path.DirectorySeparatorChar;
        if (string.Equals(workspacePath, fullRoot, comparison) ||
            !workspacePath.StartsWith(rootPrefix, comparison))
            throw new InvalidOperationException(
                $"Refusing to delete repository workspace outside the configured temporary root: {workspacePath}");
        return workspacePath;
    }

    private void PruneEmptyParents(string deletedWorkspacePath)
    {
        var fullRoot = Path.GetFullPath(context.Project.TempDirPath!)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPrefix = fullRoot + Path.DirectorySeparatorChar;
        var parent = Directory.GetParent(deletedWorkspacePath)?.FullName;
        while (parent is not null &&
               !string.Equals(parent, fullRoot, comparison) &&
               parent.StartsWith(rootPrefix, comparison))
        {
            if (!Directory.Exists(parent) || Directory.EnumerateFileSystemEntries(parent).Any()) break;
            Directory.Delete(parent);
            parent = Directory.GetParent(parent)?.FullName;
        }
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
