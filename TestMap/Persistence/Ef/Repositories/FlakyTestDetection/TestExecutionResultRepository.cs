using Microsoft.EntityFrameworkCore;
using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef.Mapping.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Repositories.FlakyTestDetection;

public class TestExecutionResultRepository
{
    private readonly TestMapDbContext _context;

    public TestExecutionResultRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<int> InsertAsync(TestExecutionResultModel result, CancellationToken cancellationToken = default)
    {
        var entity = result.ToEntity();
        _context.TestExecutionResults.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task BulkInsertAsync(
        IReadOnlyList<TestExecutionResultModel> results,
        CancellationToken cancellationToken = default)
    {
        var entities = results.Select(x => x.ToEntity()).ToList();
        _context.TestExecutionResults.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TestExecutionResultModel>> GetHistoryAsync(
        TestExecutionResultModel testIdentity,
        int historyWindowRuns,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TestExecutionResults.AsQueryable();

        query = testIdentity.TestMemberId.HasValue
            ? query.Where(x => x.TestMemberId == testIdentity.TestMemberId)
            : query.Where(x => x.TestName == testIdentity.TestName && x.FilePath == testIdentity.FilePath);

        var entities = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(historyWindowRuns)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }
}
