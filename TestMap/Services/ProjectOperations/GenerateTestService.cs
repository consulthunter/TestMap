using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class GenerateTestService :  IGenerateTestService
{
    private readonly ProjectModel _projectModel;
    private readonly string _containerName;
    public GenerateTestService(ProjectModel project, string testProvider)
    {
        _projectModel = project;
        _containerName = _projectModel.RepoName.ToLower() + "-testing";
        
    }

    public async Task GenerateTestAsync()
    {
        
    }
}