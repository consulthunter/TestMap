using Microsoft.EntityFrameworkCore;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.Persistence.Ef.Repositories.Coverage;

public class MemberCoverageRepository
{
    private readonly TestMapDbContext _context;

    public MemberCoverageRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<MemberCoverageModel>> GetAllAsync()
    {
        var entities = await _context.MemberCoverages.ToListAsync();
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<MemberCoverageModel?> GetByIdAsync(int id)
    {
        var entity = await _context.MemberCoverages.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(MemberCoverageModel model, int? memberId, int coverageReportId)
    {
        var existing = await FindExistingAsync(model, memberId, coverageReportId);

        return await InsertOrUpdateAsync(model, memberId, coverageReportId, existing);
    }

    public async Task<int> InsertOrUpdateAsync(
        MemberCoverageModel model,
        int? memberId,
        int coverageReportId,
        MemberCoverageEntity? existing)
    {
        existing ??= await FindExistingAsync(model, memberId, coverageReportId);

        if (existing != null)
        {
            if (existing.MemberId != memberId || HasChanged(existing, model))
            {
                existing.MemberId = memberId;
                Apply(existing, model);
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var entity = model.ToEntity(memberId, coverageReportId);
        _context.MemberCoverages.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public static bool HasChanged(MemberCoverageEntity entity, MemberCoverageModel model)
    {
        return entity.LineRate != SanitizeDouble(model.LineRate) ||
               entity.BranchRate != SanitizeDouble(model.BranchRate) ||
               entity.ObjectCoverageId != model.ObjectCoverageId ||
               entity.SourceOrdinal != model.SourceOrdinal ||
               entity.Name != model.Name ||
               entity.Signature != model.Signature ||
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

    public static void Apply(MemberCoverageEntity entity, MemberCoverageModel model)
    {
        entity.LineRate = SanitizeDouble(model.LineRate);
        entity.BranchRate = SanitizeDouble(model.BranchRate);
        entity.ObjectCoverageId = model.ObjectCoverageId;
        entity.SourceOrdinal = model.SourceOrdinal;
        entity.Name = model.Name;
        entity.Signature = model.Signature;
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

    private Task<MemberCoverageEntity?> FindExistingAsync(
        MemberCoverageModel model,
        int? memberId,
        int coverageReportId)
    {
        return model.SourceOrdinal >= 0 && model.ObjectCoverageId.HasValue
            ? _context.MemberCoverages.FirstOrDefaultAsync(x =>
                x.ObjectCoverageId == model.ObjectCoverageId && x.SourceOrdinal == model.SourceOrdinal)
            : _context.MemberCoverages.FirstOrDefaultAsync(x =>
                x.MemberId == memberId && x.CoverageReportId == coverageReportId && x.SourceOrdinal < 0);
    }

    public static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}
