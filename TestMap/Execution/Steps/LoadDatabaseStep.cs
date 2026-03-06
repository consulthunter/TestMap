using TestMap.App;
using TestMap.Services.Database;

namespace TestMap.Execution.Steps;

public class LoadDatabaseStep : IPipelineStep
{
    private readonly ISqliteDatabaseService _sqliteDatabaseService;
    public LoadDatabaseStep(ISqliteDatabaseService sqliteDatabaseService)
    {
        _sqliteDatabaseService = sqliteDatabaseService;
    }
    
    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _sqliteDatabaseService.InitializeAsync();
    }
    
}