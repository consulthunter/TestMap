using TestMap.App;
using TestMap.Services.ProjectDiscovery;

namespace TestMap.Execution.Steps;

public class CheckProjectsStep : IPipelineStep
{
    private ICheckProjectsService _checkProjectsService;

    public CheckProjectsStep(ICheckProjectsService checkProjectsService)
    {
        _checkProjectsService = checkProjectsService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _checkProjectsService.ProcessRepositoryAsync();
    }
}