using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class CloneRepoService
{
    private Models.TestMap _testMap;

    public CloneRepoService(Models.TestMap testMap)
    {
        _testMap = testMap;
    }
    public async Task CloneRepoAsync()
    {
        // Clone repository
        if (Directory.Exists(_testMap.ProjectModel.DirectoryPath))
        {
            try
            {
                // script
                var scriptCd = $"cd {_testMap.ProjectModel.DirectoryPath}";
                var scriptClone = $"git clone {_testMap.ProjectModel.GitHubUrl}";
                var runner = new PowerShellRunner();
                
                _testMap.Logger.Information($"Cloning repository: {_testMap.ProjectModel.GitHubUrl}");
                
                await runner.RunScript(new List<string>() { scriptCd, scriptClone });
                
                _testMap.Logger.Information($"Finished cloning repository: {_testMap.ProjectModel.GitHubUrl}");
            }
            catch (Exception ex)
            {
                _testMap.Logger.Error($"Failed to clone repository: {ex.Message}");
            }
        }
        else
        {
            _testMap.Logger.Error($"Directory {_testMap.ProjectModel.DirectoryPath} does not exist.");
        }
    }
}