using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public sealed class ExperimentMatrixWorkItemRepository
{
    private readonly TestMapDbContext _context;

    public ExperimentMatrixWorkItemRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<ExperimentMatrixWorkItem?> GetByStableKeyAsync(
        string stableKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExperimentMatrixWorkItems
            .FirstOrDefaultAsync(x => x.StableKey == stableKey, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<List<ExperimentMatrixWorkItem>> GetByExperimentRunAsync(
        int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.ExperimentMatrixWorkItems
            .Where(x => x.ExperimentRunId == experimentRunId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<int> UpsertAsync(
        ExperimentMatrixWorkItem item,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.ExperimentMatrixWorkItems
            .FirstOrDefaultAsync(x => x.StableKey == item.StableKey, cancellationToken);

        if (existing == null)
        {
            var entity = item.ToEntity();
            _context.ExperimentMatrixWorkItems.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }

        existing.Status = item.Status;
        existing.StartedAt = item.StartedAt;
        existing.LastHeartbeatAt = item.LastHeartbeatAt;
        existing.CompletedAt = item.CompletedAt;
        existing.ErrorMessage = item.ErrorMessage;
        await _context.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }

    public async Task UpdateStatusAsync(
        int id,
        string status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExperimentMatrixWorkItems.FirstAsync(x => x.Id == id, cancellationToken);
        entity.Status = status;
        entity.LastHeartbeatAt = DateTime.UtcNow;

        if (status == ExperimentMatrixWorkItemStatus.Running)
            entity.StartedAt ??= DateTime.UtcNow;

        if (status is ExperimentMatrixWorkItemStatus.Completed or ExperimentMatrixWorkItemStatus.Failed or ExperimentMatrixWorkItemStatus.Skipped)
            entity.CompletedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(errorMessage))
            entity.ErrorMessage = errorMessage;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
