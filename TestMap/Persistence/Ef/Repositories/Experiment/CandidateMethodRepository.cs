using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public class CandidateMethodRepository
{
    private readonly TestMapDbContext _context;

    public CandidateMethodRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<CandidateMethod?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.CandidateMethods
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<List<CandidateMethod>> GetByExperimentRunIdAsync(int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.CandidateMethods
            .Where(c => c.ExperimentRunId == experimentRunId)
            .OrderBy(c => c.SourceMethodName)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public Task<List<CandidateMethod>> GetByExperimentAsync(int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        return GetByExperimentRunIdAsync(experimentRunId, cancellationToken);
    }

    public async Task<List<CandidateMethod>> GetUnprocessedAsync(int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.CandidateMethods
            .Where(c => c.ExperimentRunId == experimentRunId)
            .Where(c => !c.GenerationAttempts.Any())
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<int> InsertAsync(CandidateMethod candidateMethod, CancellationToken cancellationToken = default)
    {
        var entity = candidateMethod.ToEntity();
        _context.CandidateMethods.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task BulkInsertAsync(List<CandidateMethod> candidateMethods,
        CancellationToken cancellationToken = default)
    {
        var entities = candidateMethods.Select(x => x.ToEntity()).ToList();
        _context.CandidateMethods.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CandidateMethod candidateMethod, CancellationToken cancellationToken = default)
    {
        var entity = candidateMethod.ToEntity();
        _context.CandidateMethods.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.CandidateMethods.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity != null)
        {
            _context.CandidateMethods.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}