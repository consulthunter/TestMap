using Microsoft.EntityFrameworkCore;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.Persistence.Ef.Repositories.Testing;

public class TestRunRepository
{
    private readonly TestMapDbContext _context;

    public TestRunRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<TestRunModel>> GetAllAsync()
    {
        var entities = await _context.TestRuns.ToListAsync();
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<TestRunModel?> GetByIdAsync(int id)
    {
        var entity = await _context.TestRuns.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<TestRunModel?> GetByRunIdAsync(string runId)
    {
        var entity = await _context.TestRuns.FirstOrDefaultAsync(x => x.RunId == runId);
        return entity?.ToDomain();
    }

    public async Task<TestRunModel?> GetLatestBaselineAsync(int projectId)
    {
        var entity = await _context.TestRuns
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.RunId.StartsWith("baseline_"))
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(TestRunModel model, int projectId)
    {
        var existing = await _context.TestRuns.FirstOrDefaultAsync(x =>
            x.ProjectId == projectId && x.RunId == model.RunId);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.RunDate = model.RunDate;
                existing.Success = model.Success;
                existing.Coverage = model.Coverage;
                existing.MutationScore = model.MutationScore;
                existing.LogPath = model.LogPath;
                existing.FailureAnalysis = model.FailureAnalysis;
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var entity = model.ToEntity(projectId);
        _context.TestRuns.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<int> GetIdByRunIdAsync(string runId)
    {
        var entity = await _context.TestRuns.FirstOrDefaultAsync(x => x.RunId == runId);
        return entity?.Id ?? 0;
    }

    private static bool HasChanged(Entities.Testing.TestRunEntity entity, TestRunModel model)
    {
        return entity.RunDate != model.RunDate ||
               entity.Success != model.Success ||
               entity.Coverage != model.Coverage ||
               entity.MutationScore != model.MutationScore ||
               entity.LogPath != model.LogPath ||
               entity.FailureAnalysis?.Category != model.FailureAnalysis?.Category ||
               entity.FailureAnalysis?.Stage != model.FailureAnalysis?.Stage ||
               entity.FailureAnalysis?.Summary != model.FailureAnalysis?.Summary ||
               entity.FailureAnalysis?.RemediationSuggestion != model.FailureAnalysis?.RemediationSuggestion ||
               entity.FailureAnalysis?.Evidence != model.FailureAnalysis?.Evidence ||
               entity.FailureAnalysis?.Source != model.FailureAnalysis?.Source ||
               entity.FailureAnalysis?.Confidence != model.FailureAnalysis?.Confidence;
    }
}
