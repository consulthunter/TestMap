using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public class ExperimentRunRepository
{
    private readonly TestMapDbContext _context;

    public ExperimentRunRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<ExperimentRun?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExperimentRuns
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<ExperimentRun?> GetWithCandidatesAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExperimentRuns
            .Include(e => e.CandidateMethods)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<List<ExperimentRun>> GetByProjectIdAsync(int projectId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.ExperimentRuns
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.StartTime)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<List<ExperimentRun>> GetActiveExperimentsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.ExperimentRuns
            .Where(e => e.EndTime == null)
            .OrderByDescending(e => e.StartTime)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<int> InsertAsync(ExperimentRun run, CancellationToken cancellationToken = default)
    {
        var entity = run.ToEntity();
        _context.ExperimentRuns.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(ExperimentRun run, CancellationToken cancellationToken = default)
    {
        var trackedEntity = _context.ExperimentRuns.Local.FirstOrDefault(e => e.Id == run.Id);

        if (trackedEntity != null)
        {
            trackedEntity.StartTime = run.StartedAt;
            trackedEntity.EndTime = run.CompletedAt;
            trackedEntity.ProjectId = run.ProjectId;
            trackedEntity.Configuration = run.ConfigurationJson;
            trackedEntity.CandidateLimit = run.CandidateLimit;
            trackedEntity.Status = string.IsNullOrWhiteSpace(run.Status) ? "Completed" : run.Status;
        }
        else
        {
            var entity = run.ToEntity();
            _context.ExperimentRuns.Update(entity);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExperimentRuns.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity != null)
        {
            _context.ExperimentRuns.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}