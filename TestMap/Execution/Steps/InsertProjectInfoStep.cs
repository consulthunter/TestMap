using TestMap.App;
using TestMap.Services.CollectInformation;
using TestMap.Services.Database;

namespace TestMap.Execution.Steps;

public class InsertProjectInfoStep : IPipelineStep
{
    private readonly ISqliteDatabaseService _sqliteDatabaseService;
    
    public InsertProjectInfoStep(ISqliteDatabaseService sqliteDatabaseService)
    {
        _sqliteDatabaseService = sqliteDatabaseService;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _sqliteDatabaseService.InsertProjectInformation();
    }
}
