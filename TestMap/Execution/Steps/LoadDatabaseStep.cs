using TestMap.App;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories;

namespace TestMap.Execution.Steps;

public class LoadDatabaseStep : IPipelineStep
{
    private readonly TestMapDbContext _db;
    private readonly TestMapDatabaseInitializer _databaseInitializer;
    private readonly ProjectRepository _projectRepository;

    public LoadDatabaseStep(
        TestMapDbContext db,
        TestMapDatabaseInitializer databaseInitializer,
        ProjectRepository projectRepository)
    {
        _db = db;
        _databaseInitializer = databaseInitializer;
        _projectRepository = projectRepository;
    }

    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _databaseInitializer.InitializeAsync(_db);
        await _projectRepository.GetAllAsync();
    }
}
