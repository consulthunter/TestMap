using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class CloneRepoService
{
    private ProjectModel _projectModel;

    public CloneRepoService(ProjectModel projectModel)
    {
        _projectModel = projectModel;
    }
    public async Task CloneRepoAsync()
    {
        // Clone repository
        if (Directory.Exists(_projectModel.DirectoryPath))
        {
            try
            {
                // script
                var scriptCd = $"cd {_projectModel.DirectoryPath}";
                var scriptClone = $"git clone {_projectModel.GitHubUrl}";
                var runner = new PowerShellRunner();
                
                _projectModel.Logger.Information($"Cloning repository: {_projectModel.GitHubUrl}");
                
                await runner.RunScript(new List<string>() { scriptCd, scriptClone });
                
                _projectModel.Logger.Information($"Finished cloning repository: {_projectModel.GitHubUrl}");
            }
            catch (Exception ex)
            {
                _projectModel.Logger.Error($"Failed to clone repository: {ex.Message}");
            }
        }
        else
        {
            _projectModel.Logger.Error($"Directory {_projectModel.DirectoryPath} does not exist.");
        }
    }
}