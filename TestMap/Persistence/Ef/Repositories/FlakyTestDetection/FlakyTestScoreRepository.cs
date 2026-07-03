using Microsoft.EntityFrameworkCore;
using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef.Mapping.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Repositories.FlakyTestDetection;

public class FlakyTestScoreRepository
{
    private readonly TestMapDbContext _context;

    public FlakyTestScoreRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<int> InsertAsync(FlakyTestScoreModel score, CancellationToken cancellationToken = default)
    {
        var entity = score.ToEntity();
        _context.FlakyTestScores.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task BulkInsertAsync(
        IReadOnlyList<FlakyTestScoreModel> scores,
        CancellationToken cancellationToken = default)
    {
        var entities = scores.Select(x => x.ToEntity()).ToList();
        _context.FlakyTestScores.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FlakyTestScoreModel>> GetByRunIdAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.FlakyTestScores
            .Where(x => x.RunId == runId)
            .OrderByDescending(x => x.FlakinessScore)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }
}