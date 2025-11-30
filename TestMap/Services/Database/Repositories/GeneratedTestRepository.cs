using TestMap.Models;

namespace TestMap.Services.Database.Repositories;

public class GeneratedTestRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public GeneratedTestRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }
}