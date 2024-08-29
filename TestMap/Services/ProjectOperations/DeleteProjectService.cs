using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class DeleteProjectService
{
    private ProjectModel _projectModel;

    public DeleteProjectService(ProjectModel projectModel)
    {
        _projectModel = projectModel;
    }
    public async Task DeleteProjectAsync()
    {
        // Delete if the directory exists
        if (Directory.Exists(_projectModel.DirectoryPath))
        {
            try
            {
                // script
                var script = $"rm {_projectModel.DirectoryPath} -r -force";
                var runner = new PowerShellRunner();
                await runner.RunScript(new List<string>() { script });
            }
            catch (Exception ex)
            {
                _projectModel.Logger.Error($"Failed to delete repository: {ex.Message}");
            }
        }
        else
        {
            _projectModel.Logger.Error($"Directory {_projectModel.DirectoryPath} does not exist.");
        }
    }
}