using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public class GenerationAttemptRepository
{
    private readonly TestMapDbContext _context;

    public GenerationAttemptRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<GenerationAttempt?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GenerationAttempts
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<GenerationAttempt?> GetWithStepsAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GenerationAttempts
            .Include(g => g.GenerationSteps)
            .Include(g => g.TestExecution)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<List<GenerationAttempt>> GetByCandidateMethodIdAsync(int candidateMethodId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.GenerationAttempts
            .Include(g => g.GenerationSteps)
            .Include(g => g.TestExecution)
            .Where(g => g.CandidateMethodId == candidateMethodId)
            .OrderBy(g => g.StartTime)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public Task<List<GenerationAttempt>> GetByCandidateMethodAsync(int candidateMethodId,
        CancellationToken cancellationToken = default)
    {
        return GetByCandidateMethodIdAsync(candidateMethodId, cancellationToken);
    }

    public async Task<List<GenerationAttempt>> GetSuccessfulAttemptsAsync(int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.GenerationAttempts
            .Include(g => g.CandidateMethod)
            .Include(g => g.TestExecution)
            .Where(g => g.CandidateMethod != null && g.CandidateMethod.ExperimentRunId == experimentRunId)
            .Where(g => g.TestExecution != null && g.TestExecution.TestPassed)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<Dictionary<AiProvider, int>> GetSuccessCountByProviderAsync(int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.GenerationAttempts
            .Include(g => g.CandidateMethod)
            .Include(g => g.TestExecution)
            .Where(g => g.CandidateMethod != null && g.CandidateMethod.ExperimentRunId == experimentRunId)
            .Where(g => g.TestExecution != null && g.TestExecution.TestPassed)
            .GroupBy(g => g.ProviderName)
            .Select(group => new { Provider = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            x => Enum.TryParse<AiProvider>(x.Provider, true, out var provider) ? provider : AiProvider.OpenAi,
            x => x.Count);
    }

    public async Task<int> InsertAsync(GenerationAttempt attempt, CancellationToken cancellationToken = default)
    {
        var entity = attempt.ToEntity();
        _context.GenerationAttempts.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task BulkInsertAsync(List<GenerationAttempt> attempts, CancellationToken cancellationToken = default)
    {
        var entities = attempts.Select(x => x.ToEntity()).ToList();
        _context.GenerationAttempts.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(GenerationAttempt attempt, CancellationToken cancellationToken = default)
    {
        var entity = attempt.ToEntity();
        _context.GenerationAttempts.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GenerationAttempts.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (entity != null)
        {
            _context.GenerationAttempts.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}