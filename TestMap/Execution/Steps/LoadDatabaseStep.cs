using TestMap.App;
using TestMap.Persistence.Ef.Repositories;

namespace TestMap.Execution.Steps;

public class LoadDatabaseStep : IPipelineStep
{
    private readonly ProjectRepository _projectRepository;

    public LoadDatabaseStep(ProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _projectRepository.GetAllAsync();
    }
}