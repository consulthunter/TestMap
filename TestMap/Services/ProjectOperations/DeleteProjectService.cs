namespace TestMap.Services.ProjectOperations;

public class DeleteProjectService
{
    private Models.TestMap _testMap;

    public DeleteProjectService(Models.TestMap testMap)
    {
        _testMap = testMap;
    }
    public async Task DeleteProjectAsync()
    {
        // Delete if the directory exists
        if (Directory.Exists(_testMap.ProjectModel.DirectoryPath))
        {
            try
            {
                // script
                var script = $"rm {_testMap.ProjectModel.DirectoryPath} -r -force";
                var runner = new PowerShellRunner();
                await runner.RunScript(new List<string>() { script });
            }
            catch (Exception ex)
            {
                _testMap.Logger.Error($"Failed to delete repository: {ex.Message}");
            }
        }
        else
        {
            _testMap.Logger.Error($"Directory {_testMap.ProjectModel.DirectoryPath} does not exist.");
        }
    }
}