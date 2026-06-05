using Microsoft.EntityFrameworkCore;
using TestMap.Models.AgentTools;
using TestMap.Persistence.Ef.Mapping.AgentTools;

namespace TestMap.Persistence.Ef.Repositories.AgentTools;

public class ToolAttemptRepository
{
    private readonly TestMapDbContext _context;

    public ToolAttemptRepository(TestMapDbContext context) => _context = context;

    /// <summary>Inserts a new attempt and returns the auto-assigned id.</summary>
    public async Task<int> InsertAsync(ToolAttempt attempt, CancellationToken ct = default)
    {
        var entity = attempt.ToEntity();
        _context.ToolAttempts.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    /// <summary>Updates only the run_status column for the given attempt id.</summary>
    public async Task UpdateStatusAsync(
        int id,
        ToolRunStatus status,
        CancellationToken ct = default)
    {
        var entity = await _context.ToolAttempts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException($"ToolAttempt {id} not found.");
        entity.RunStatus = status.ToString();
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Full update of an existing attempt record.</summary>
    public async Task UpdateAsync(ToolAttempt attempt, CancellationToken ct = default)
    {
        _context.ChangeTracker.Clear();
        var entity = await _context.ToolAttempts.FirstOrDefaultAsync(x => x.Id == attempt.Id, ct)
            ?? throw new InvalidOperationException($"ToolAttempt {attempt.Id} not found.");
        _context.Entry(entity).CurrentValues.SetValues(attempt.ToEntity());
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Returns a single attempt by id, or null if not found.</summary>
    public async Task<ToolAttempt?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.ToolAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity?.ToDomain();
    }

    /// <summary>Returns all attempts for an experiment run, ordered by StartedAt.</summary>
    public async Task<List<ToolAttempt>> GetByExperimentRunAsync(
        int experimentRunId,
        CancellationToken ct = default)
    {
        var entities = await _context.ToolAttempts
            .AsNoTracking()
            .Where(x => x.ExperimentRunId == experimentRunId)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(ct);
        return entities.Select(x => x.ToDomain()).ToList();
    }

    /// <summary>Returns all attempts for a candidate method, ordered by StartedAt.</summary>
    public async Task<List<ToolAttempt>> GetByCandidateAsync(
        int candidateMethodId,
        CancellationToken ct = default)
    {
        var entities = await _context.ToolAttempts
            .AsNoTracking()
            .Where(x => x.CandidateMethodId == candidateMethodId)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(ct);
        return entities.Select(x => x.ToDomain()).ToList();
    }
}
