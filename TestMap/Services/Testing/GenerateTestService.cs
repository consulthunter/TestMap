using TestMap.Models;
using TestMap.Models.Configuration;

namespace TestMap.Services.ProjectOperations;

public class GenerateTestService :  IGenerateTestService
{
    private readonly ProjectModel _projectModel;
    private readonly GenerationConfig _generationConfig;
    public GenerateTestService(ProjectModel project, GenerationConfig  config)
    {
        _projectModel = project;
        _generationConfig = config;
        
    }

    public async Task GenerateTestAsync()
    {
        
    }
}