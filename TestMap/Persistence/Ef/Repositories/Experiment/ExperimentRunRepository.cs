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
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<ExperimentRun?> GetWithCandidatesAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExperimentRuns
            .AsNoTracking()
            .Include(e => e.CandidateMethods)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<List<ExperimentRun>> GetByProjectIdAsync(int projectId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.ExperimentRuns
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.StartTime)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<List<ExperimentRun>> GetActiveExperimentsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.ExperimentRuns
            .AsNoTracking()
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
        _context.ChangeTracker.Clear();

        var trackedEntity = await _context.ExperimentRuns
            .FirstOrDefaultAsync(e => e.Id == run.Id, cancellationToken);

        if (trackedEntity == null)
            throw new InvalidOperationException($"Experiment run '{run.Id}' was not found.");

        trackedEntity.StartTime = run.StartedAt;
        trackedEntity.EndTime = run.CompletedAt;
        trackedEntity.ProjectId = run.ProjectId;
        trackedEntity.Objective = run.Objective;
        trackedEntity.CandidateSelectionStrategy = run.CandidateSelectionStrategy;
        trackedEntity.Configuration = run.ConfigurationJson;
        trackedEntity.ResultsFilePath = run.ResultsFilePath;
        trackedEntity.CandidateLimit = run.CandidateLimit;
        trackedEntity.Status = string.IsNullOrWhiteSpace(run.Status) ? "Completed" : run.Status;

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
