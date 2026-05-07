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

    public async Task<int> InsertOrUpdateAsync(MemberCoverageModel model, int memberId, int coverageReportId)
    {
        var existing = await _context.MemberCoverages.FirstOrDefaultAsync(x =>
            x.MemberId == memberId && x.CoverageReportId == coverageReportId);

        return await InsertOrUpdateAsync(model, memberId, coverageReportId, existing);
    }

    public async Task<int> InsertOrUpdateAsync(
        MemberCoverageModel model,
        int memberId,
        int coverageReportId,
        MemberCoverageEntity? existing)
    {
        existing ??= await _context.MemberCoverages.FirstOrDefaultAsync(x =>
            x.MemberId == memberId && x.CoverageReportId == coverageReportId);

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

        var entity = model.ToEntity(memberId, coverageReportId);
        _context.MemberCoverages.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public static bool HasChanged(MemberCoverageEntity entity, MemberCoverageModel model)
    {
        return entity.LineRate != SanitizeDouble(model.LineRate) ||
               entity.BranchRate != SanitizeDouble(model.BranchRate) ||
               entity.Complexity != SanitizeDouble(model.ComplexityValue);
    }

    public static void Apply(MemberCoverageEntity entity, MemberCoverageModel model)
    {
        entity.LineRate = SanitizeDouble(model.LineRate);
        entity.BranchRate = SanitizeDouble(model.BranchRate);
        entity.Complexity = SanitizeDouble(model.ComplexityValue);
    }

    public static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}