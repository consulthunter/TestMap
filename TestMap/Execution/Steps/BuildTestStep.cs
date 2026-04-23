using TestMap.App;
using TestMap.Services.Testing;

namespace TestMap.Execution.Steps;

public class BuildTestStep : IPipelineStep
{
    private readonly IBuildTestService _buildTestService;
    
    public BuildTestStep(IBuildTestService buildTestService)
    {
        _buildTestService = buildTestService;
    }
    
    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        var sols = context?.Project.Solutions
            .Select(x => x.FilePath)
            .ToList();
        
        await _buildTestService.BuildTestAsync(
            BuildTestRunRequest.CreateBaseline(sols ?? []));
    }
}
