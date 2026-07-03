using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public class GenerationStepRepository
{
    private readonly TestMapDbContext _context;

    public GenerationStepRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<GenerationStep?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GenerationSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<List<GenerationStep>> GetByAttemptIdAsync(int attemptId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.GenerationSteps
            .AsNoTracking()
            .Where(s => s.GenerationAttemptId == attemptId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<GenerationStep?> GetByStepTypeAsync(int attemptId, GenerationStepType stepType,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.GenerationSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.GenerationAttemptId == attemptId && s.StepName == stepType.ToString(),
                cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<Dictionary<GenerationStepType, int>> GetAverageTokensByStepTypeAsync(int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var attempts = await _context.GenerationAttempts
            .AsNoTracking()
            .Include(a => a.CandidateMethod)
            .Where(a => a.CandidateMethod != null && a.CandidateMethod.ExperimentRunId == experimentRunId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var rows = await _context.GenerationSteps
            .AsNoTracking()
            .Where(s => attempts.Contains(s.GenerationAttemptId))
            .GroupBy(s => s.StepName)
            .Select(group => new { StepName = group.Key, AvgTokens = (int)group.Average(s => s.TokensUsed) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            x => Enum.TryParse<GenerationStepType>(x.StepName, true, out var stepType)
                ? stepType
                : GenerationStepType.Scenario,
            x => x.AvgTokens);
    }

    public async Task<int> InsertAsync(GenerationStep step, CancellationToken cancellationToken = default)
    {
        var entity = step.ToEntity();
        _context.GenerationSteps.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task BulkInsertAsync(List<GenerationStep> steps, CancellationToken cancellationToken = default)
    {
        var entities = steps.Select(x => x.ToEntity()).ToList();
        _context.GenerationSteps.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(GenerationStep step, CancellationToken cancellationToken = default)
    {
        _context.ChangeTracker.Clear();

        var entity = await _context.GenerationSteps
            .FirstOrDefaultAsync(s => s.Id == step.Id, cancellationToken);

        if (entity == null)
            throw new InvalidOperationException($"Generation step '{step.Id}' was not found.");

        _context.Entry(entity).CurrentValues.SetValues(step.ToEntity());
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.GenerationSteps.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity != null)
        {
            _context.GenerationSteps.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
