using Microsoft.EntityFrameworkCore;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;
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

    public async Task<int> InsertOrUpdateAsync(ObjectCoverageModel model, int? objectId, int coverageReportId)
    {
        var existing = await FindExistingAsync(model, objectId, coverageReportId);

        return await InsertOrUpdateAsync(model, objectId, coverageReportId, existing);
    }

    public async Task<int> InsertOrUpdateAsync(
        ObjectCoverageModel model,
        int? objectId,
        int coverageReportId,
        ObjectCoverageEntity? existing)
    {
        existing ??= await FindExistingAsync(model, objectId, coverageReportId);

        if (existing != null)
        {
            if (existing.ObjectId != objectId || HasChanged(existing, model))
            {
                existing.ObjectId = objectId;
                Apply(existing, model);
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var entity = model.ToEntity(objectId, coverageReportId);
        _context.ObjectCoverages.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public static bool HasChanged(ObjectCoverageEntity entity, ObjectCoverageModel model)
    {
        return entity.LineRate != SanitizeDouble(model.LineRate) ||
               entity.BranchRate != SanitizeDouble(model.BranchRate) ||
               entity.SourceOrdinal != model.SourceOrdinal ||
               entity.PackageName != model.PackageName ||
               entity.Name != model.Name ||
               entity.Filename != model.Filename ||
               entity.AttributionStatus != model.AttributionStatus ||
               entity.AttributionReason != model.AttributionReason ||
               entity.LinesCovered != model.LinesCovered ||
               entity.LinesValid != model.LinesValid ||
               entity.BranchesCovered != model.BranchesCovered ||
               entity.BranchesValid != model.BranchesValid ||
               entity.LineCountsAvailable != model.LineCountsAvailable ||
               entity.BranchCountsAvailable != model.BranchCountsAvailable ||
               entity.Complexity != SanitizeDouble(model.ComplexityValue);
    }

    public static void Apply(ObjectCoverageEntity entity, ObjectCoverageModel model)
    {
        entity.LineRate = SanitizeDouble(model.LineRate);
        entity.BranchRate = SanitizeDouble(model.BranchRate);
        entity.SourceOrdinal = model.SourceOrdinal;
        entity.PackageName = model.PackageName;
        entity.Name = model.Name;
        entity.Filename = model.Filename;
        entity.AttributionStatus = model.AttributionStatus;
        entity.AttributionReason = model.AttributionReason;
        entity.LinesCovered = model.LinesCovered;
        entity.LinesValid = model.LinesValid;
        entity.BranchesCovered = model.BranchesCovered;
        entity.BranchesValid = model.BranchesValid;
        entity.LineCountsAvailable = model.LineCountsAvailable;
        entity.BranchCountsAvailable = model.BranchCountsAvailable;
        entity.Complexity = SanitizeDouble(model.ComplexityValue);
    }

    private Task<ObjectCoverageEntity?> FindExistingAsync(
        ObjectCoverageModel model,
        int? objectId,
        int coverageReportId)
    {
        return model.SourceOrdinal >= 0
            ? _context.ObjectCoverages.FirstOrDefaultAsync(x =>
                x.CoverageReportId == coverageReportId && x.SourceOrdinal == model.SourceOrdinal)
            : _context.ObjectCoverages.FirstOrDefaultAsync(x =>
                x.ObjectId == objectId && x.CoverageReportId == coverageReportId && x.SourceOrdinal < 0);
    }

    public static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}
