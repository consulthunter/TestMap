using TestMap.App;

using TestMap.Persistence.Ef.Repositories;

namespace TestMap.Execution.Steps;

public class InsertProjectInfoStep : IPipelineStep
{
    private readonly ProjectRepository _projectRepository;
    
    public InsertProjectInfoStep(ProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        if (context == null)
        {
            return;
        }
        
        await _projectRepository.InsertOrUpdateAsync(context.Project);
    }
}
