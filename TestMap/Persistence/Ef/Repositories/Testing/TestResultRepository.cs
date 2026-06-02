using Microsoft.EntityFrameworkCore;
using TestMap.Models.FlakyTestDetection;
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

    public async Task<IReadOnlyList<TestExecutionResultModel>> GetHistoryAsync(
        TestExecutionResultModel testIdentity,
        int historyWindowRuns,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TestResults
            .AsNoTracking()
            .Include(x => x.TestRun)
            .AsQueryable();

        query = testIdentity.TestMemberId.HasValue
            ? query.Where(x => x.MethodId == testIdentity.TestMemberId.Value)
            : query.Where(x => x.TestName == testIdentity.TestName);

        var entities = await query
            .OrderByDescending(x => x.TestRun != null ? x.TestRun.CreatedAt : null)
            .ThenByDescending(x => x.Id)
            .Take(historyWindowRuns)
            .OrderBy(x => x.TestRun != null ? x.TestRun.CreatedAt : null)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return entities.Select(x => new TestExecutionResultModel
        {
            Id = x.Id,
            RunId = x.RunId,
            TestMemberId = x.MethodId > 0 ? x.MethodId : null,
            TestName = x.TestName,
            Outcome = x.Outcome,
            DurationMs = x.Duration.TotalMilliseconds,
            ErrorMessage = x.ErrorMessage,
            ErrorStackTrace = x.StackTrace,
            CreatedAt = x.TestRun?.CreatedAt ?? DateTime.UtcNow
        }).ToList();
    }
}
