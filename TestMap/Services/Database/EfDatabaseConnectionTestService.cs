using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities;

namespace TestMap.Services.Database;

public class EfSmokeTestService
{
    private readonly TestMapDbContext _dbContext;

    public EfSmokeTestService(TestMapDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
        Console.WriteLine($"EF CanConnect: {canConnect}");

        var projectCount = await _dbContext.Set<ProjectEntity>().CountAsync(cancellationToken);
        Console.WriteLine($"EF Project count: {projectCount}");

        var firstProjects = await _dbContext.Set<ProjectEntity>()
            .OrderBy(x => x.Id)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var project in firstProjects)
        {
            Console.WriteLine($"[{project.Id}] {project.Owner}/{project.RepoName}");
        }
    }
}
