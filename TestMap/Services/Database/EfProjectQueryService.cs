using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;

namespace TestMap.Services.Database;

public class EfProjectQueryService : IEfProjectQueryService
{
    private readonly TestMapDbContext _dbContext;

    public EfProjectQueryService(TestMapDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> GetProjectCountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects.CountAsync(cancellationToken);
    }

    public async Task<List<string>> GetProjectNamesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .Select(x => $"{x.Owner}/{x.RepoName}")
            .ToListAsync(cancellationToken);
    }
}