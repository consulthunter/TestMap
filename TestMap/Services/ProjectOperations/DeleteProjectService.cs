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
    /// Removes the project from the Temp directory
    /// using a custom script
    /// </summary>
    public async Task DeleteProjectAsync()
    {
        // Delete if the directory exists
        if (Directory.Exists(projectModel.DirectoryPath))
            try
            {
                // script
                var runner = new ScriptRunner();
                projectModel.Logger.Information($"Deleting repository: {projectModel.GitHubUrl}");
                await runner.RunScriptAsync([projectModel.DirectoryPath],projectModel.Scripts["Delete"]);
                projectModel.Logger.Information($"Finished deleting repository: {projectModel.GitHubUrl}");
            }
            catch (Exception ex)
            {
                projectModel.Logger.Error($"Failed to delete repository: {ex.Message}");
            }
        else
            projectModel.Logger.Error($"Directory {projectModel.DirectoryPath} does not exist.");
    }
}