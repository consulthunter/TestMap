using Microsoft.EntityFrameworkCore;
using TestMap.Models.AgentTools;
using TestMap.Persistence.Ef.Mapping.AgentTools;

namespace TestMap.Persistence.Ef.Repositories.AgentTools;

public class ToolAttemptGeneratedTestRepository
{
    private readonly TestMapDbContext _context;

    public ToolAttemptGeneratedTestRepository(TestMapDbContext context) => _context = context;

    public async Task<int> InsertAsync(ToolAttemptGeneratedTest row, CancellationToken ct = default)
    {
        var entity = row.ToEntity();
        _context.ToolAttemptGeneratedTests.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task InsertManyAsync(
        IEnumerable<ToolAttemptGeneratedTest> rows,
        CancellationToken ct = default)
    {
        var entities = rows.Select(r => r.ToEntity()).ToList();
        if (entities.Count == 0) return;
        _context.ToolAttemptGeneratedTests.AddRange(entities);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ToolAttemptGeneratedTest>> GetByAttemptIdAsync(
        int toolAttemptId,
        CancellationToken ct = default)
    {
        var entities = await _context.ToolAttemptGeneratedTests
            .AsNoTracking()
            .Where(x => x.ToolAttemptId == toolAttemptId)
            .ToListAsync(ct);
        return entities.Select(x => x.ToDomain()).ToList();
    }
}
