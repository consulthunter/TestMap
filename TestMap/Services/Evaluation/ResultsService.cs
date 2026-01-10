using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Services.Database;

namespace TestMap.Services.Evaluation;

public class ResultsService : IResultsService
{
    private readonly ProjectModel _projectModel;
    private readonly TestMapConfig _testMapConfig;
    private readonly SqliteDatabaseService _sqliteDatabaseService;
    
    public ResultsService(ProjectModel projectModel, TestMapConfig config, SqliteDatabaseService sqliteDatabaseService)
    {
        _projectModel = projectModel;
        _testMapConfig = config;
        _sqliteDatabaseService = sqliteDatabaseService;
    }
    
    public async Task ResultsAsync()
    {
        
    }
}