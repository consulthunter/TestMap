using Microsoft.EntityFrameworkCore;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.Persistence.Ef.Repositories.Coverage;

public class ObjectCoverageRepository
{
    private readonly TestMapDbContext _context;

    public ObjectCoverageRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<ObjectCoverageModel>> GetAllAsync()
    {
        var entities = await _context.ObjectCoverages.ToListAsync();
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<ObjectCoverageModel?> GetByIdAsync(int id)
    {
        var entity = await _context.ObjectCoverages.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(ObjectCoverageModel model, int objectId, int coverageReportId)
    {
        var existing = await _context.ObjectCoverages.FirstOrDefaultAsync(x =>
            x.ObjectId == objectId && x.CoverageReportId == coverageReportId);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.LineRate = SanitizeDouble(model.LineRate);
                existing.BranchRate = SanitizeDouble(model.BranchRate);
                existing.Complexity = SanitizeDouble(model.ComplexityValue);
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var entity = model.ToEntity(objectId, coverageReportId);
        _context.ObjectCoverages.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    private static bool HasChanged(Entities.Coverage.ObjectCoverageEntity entity, ObjectCoverageModel model)
    {
        return entity.LineRate != SanitizeDouble(model.LineRate) ||
               entity.BranchRate != SanitizeDouble(model.BranchRate) ||
               entity.Complexity != SanitizeDouble(model.ComplexityValue);
    }

    private static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}
