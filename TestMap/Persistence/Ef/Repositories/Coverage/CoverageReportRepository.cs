using Microsoft.EntityFrameworkCore;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.Persistence.Ef.Repositories.Coverage;

public class CoverageReportRepository
{
    private readonly TestMapDbContext _context;

    public CoverageReportRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<CoverageReportModel>> GetAllAsync()
    {
        var entities = await _context.CoverageReports.ToListAsync();
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<CoverageReportModel?> GetByIdAsync(int id)
    {
        var entity = await _context.CoverageReports.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<CoverageReportModel?> GetLatestByProjectIdAsync(int projectId)
    {
        var entity = await _context.CoverageReports
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(CoverageReportModel model, int projectId)
    {
        var existing = await _context.CoverageReports.FirstOrDefaultAsync(x =>
            x.ProjectId == projectId && x.Timestamp == model.Timestamp);

        return await InsertOrUpdateAsync(model, projectId, existing);
    }

    public async Task<int> InsertOrUpdateAsync(
        CoverageReportModel model,
        int projectId,
        CoverageReportEntity? existing)
    {
        existing ??= await _context.CoverageReports.FirstOrDefaultAsync(x =>
            x.ProjectId == projectId && x.Timestamp == model.Timestamp);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.LineRate = SanitizeDouble(model.LineRate);
                existing.BranchRate = SanitizeDouble(model.BranchRate);
                existing.Complexity = SanitizeDouble(model.ComplexityValue);
                existing.Version = model.Version;
                existing.LinesCovered = model.LinesCovered;
                existing.LinesValid = model.LinesValid;
                existing.BranchesCovered = model.BranchesCovered;
                existing.BranchesValid = model.BranchesValid;
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var entity = model.ToEntity(projectId);
        _context.CoverageReports.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<bool> HasCoverageReportsAsync(int projectId)
    {
        return await _context.CoverageReports.AnyAsync(x => x.ProjectId == projectId);
    }

    private static bool HasChanged(CoverageReportEntity entity, CoverageReportModel model)
    {
        return entity.LineRate != SanitizeDouble(model.LineRate) ||
               entity.BranchRate != SanitizeDouble(model.BranchRate) ||
               entity.Complexity != SanitizeDouble(model.ComplexityValue) ||
               entity.Version != model.Version ||
               entity.LinesCovered != model.LinesCovered ||
               entity.LinesValid != model.LinesValid ||
               entity.BranchesCovered != model.BranchesCovered ||
               entity.BranchesValid != model.BranchesValid;
    }

    private static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}