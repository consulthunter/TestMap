using TestMap.App;
using TestMap.Services.Database;
namespace TestMap.Execution.Steps;

public class EfSmokeTestStep : IPipelineStep
{
    private readonly EfSmokeTestService _efSmokeTestService;

    public EfSmokeTestStep(EfSmokeTestService efSmokeTestService)
    {
        _efSmokeTestService = efSmokeTestService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _efSmokeTestService.RunAsync();
    }
}
