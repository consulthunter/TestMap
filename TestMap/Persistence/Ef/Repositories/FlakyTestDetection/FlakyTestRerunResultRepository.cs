using Microsoft.EntityFrameworkCore;
using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef.Mapping.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Repositories.FlakyTestDetection;

public class FlakyTestRerunResultRepository
{
    private readonly TestMapDbContext _context;

    public FlakyTestRerunResultRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task BulkInsertAsync(
        IReadOnlyList<FlakyTestRerunResultModel> results,
        CancellationToken cancellationToken = default)
    {
        var entities = results.Select(x => x.ToEntity()).ToList();
        _context.FlakyTestRerunResults.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FlakyTestRerunResultModel>> GetByRunIdAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.FlakyTestRerunResults
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.TestExecutionResultId)
            .ThenBy(x => x.AttemptNumber)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }
}