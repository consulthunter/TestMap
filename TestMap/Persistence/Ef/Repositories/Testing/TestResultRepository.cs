using Microsoft.EntityFrameworkCore;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.Persistence.Ef.Repositories.Testing;

public class TestResultRepository
{
    private readonly TestMapDbContext _context;

    public TestResultRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<TestResultModel>> GetAllAsync()
    {
        var entities = await _context.TestResults.ToListAsync();
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<TestResultModel?> GetByIdAsync(int id)
    {
        var entity = await _context.TestResults.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task InsertAsync(IEnumerable<TestResultModel> models, int testRunId)
    {
        var entities = models.Select(x => x.ToEntity(testRunId));
        await _context.TestResults.AddRangeAsync(entities);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasPassingTestsAsync(int projectId)
    {
        var testRunIds = await _context.TestRuns
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Id)
            .ToListAsync();

        return await _context.TestResults.AnyAsync(x =>
            testRunIds.Contains(x.TestRunId) &&
            x.Outcome.ToLower() == "passed");
    }
}
