using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Persistence.Ef.Repositories.Code;

public class CodeMetricRepository
{
    private readonly TestMapDbContext _context;

    public CodeMetricRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<CodeMetricsModel>> GetAllAsync()
    {
        return await _context.CodeMetrics
            .Select(x => x.ToDomain())
            .ToListAsync();
    }

    public async Task<CodeMetricsModel?> GetByIdAsync(int id)
    {
        var entity = await _context.CodeMetrics.FindAsync(id);
        return entity?.ToDomain();
    }

    // public async Task<CodeMetricsModel?> GetByContentHashAsync(string contentHash)
    // {
    //     var entity = await _context.CodeMetrics.FirstOrDefaultAsync(x => x.ContentHash == contentHash);
    //     return entity?.ToDomain();
    // }

    public async Task<int> InsertOrUpdateAsync(CodeMetricsModel model)
    {
        var existing = await _context.CodeMetrics.FirstOrDefaultAsync(x =>
            x.EntityId == model.EntityId && x.EntityType == model.EntityType);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.ClassCoupling = model.ClassCoupling;
                existing.SourceLinesOfCode = model.SourceLinesOfCode;
                existing.CyclomaticComplexity = model.CyclomaticComplexity;
                existing.DepthOfInheritance = model.DepthOfInheritance;
                existing.MaintainabilityIndex = model.MaintainabilityIndex;
                existing.ExecutableLinesOfCode = model.ExecutableLinesOfCode;
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var entityId = model.EntityId;
        var entityType = model.EntityType;
        var entity = model.ToEntity(entityId, entityType);
        _context.CodeMetrics.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public static bool HasChanged(CodeMetricEntity existing, CodeMetricsModel model)
    {
        return existing.ClassCoupling != model.ClassCoupling ||
               existing.SourceLinesOfCode != model.SourceLinesOfCode ||
               existing.ExecutableLinesOfCode != model.ExecutableLinesOfCode ||
               existing.CyclomaticComplexity != model.CyclomaticComplexity ||
               existing.DepthOfInheritance != model.DepthOfInheritance ||
               existing.MaintainabilityIndex != model.MaintainabilityIndex;
    }

    public static void Apply(CodeMetricEntity existing, CodeMetricsModel model)
    {
        existing.ClassCoupling = model.ClassCoupling;
        existing.SourceLinesOfCode = model.SourceLinesOfCode;
        existing.CyclomaticComplexity = model.CyclomaticComplexity;
        existing.DepthOfInheritance = model.DepthOfInheritance;
        existing.MaintainabilityIndex = model.MaintainabilityIndex;
        existing.ExecutableLinesOfCode = model.ExecutableLinesOfCode;
    }
}