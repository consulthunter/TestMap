using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public class CandidateInventoryRepository
{
    private readonly TestMapDbContext _context;

    public CandidateInventoryRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task ReplaceForProjectStrategyAsync(
        int projectId,
        TargetSelectionStrategy strategy,
        IReadOnlyCollection<CandidateInventoryItem> items,
        CancellationToken cancellationToken = default)
    {
        var strategyName = strategy.ToString();
        var existing = await _context.CandidateInventory
            .Where(x => x.ProjectId == projectId && x.SelectionStrategy == strategyName)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0) _context.CandidateInventory.RemoveRange(existing);

        if (items.Count > 0)
        {
            var entities = items.Select(x => x.ToEntity()).ToList();
            _context.CandidateInventory.AddRange(entities);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        int projectId,
        TargetSelectionStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var strategyName = strategy.ToString();
        return _context.CandidateInventory
            .CountAsync(x => x.ProjectId == projectId && x.SelectionStrategy == strategyName, cancellationToken);
    }

    public Task<int> CountEligibleAsync(
        int projectId,
        TargetSelectionStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var strategyName = strategy.ToString();
        return _context.CandidateInventory
            .CountAsync(x =>
                    x.ProjectId == projectId &&
                    x.SelectionStrategy == strategyName &&
                    x.IsExperimentEligible,
                cancellationToken);
    }
}
