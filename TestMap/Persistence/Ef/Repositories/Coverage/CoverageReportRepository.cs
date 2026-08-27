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
        var existing = await FindExistingAsync(model, projectId);

        return await InsertOrUpdateAsync(model, projectId, existing);
    }

    public async Task<int> InsertOrUpdateAsync(
        CoverageReportModel model,
        int projectId,
        CoverageReportEntity? existing)
    {
        existing ??= await FindExistingAsync(model, projectId);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.LineRate = SanitizeDouble(model.LineRate);
                existing.BranchRate = SanitizeDouble(model.BranchRate);
                existing.Complexity = SanitizeDouble(model.ComplexityValue);
                existing.Version = model.Version;
                existing.RunId = model.RunId;
                existing.CollectionStatus = model.CollectionStatus;
                existing.CollectionReason = model.CollectionReason;
                existing.SuccessfulCollector = model.SuccessfulCollector;
                existing.CollectionMetadataJson = model.CollectionMetadataJson;
                existing.HasUsableCoverage = model.HasUsableCoverage;
                existing.LineCountsAvailable = model.LineCountsAvailable;
                existing.BranchCountsAvailable = model.BranchCountsAvailable;
                existing.MeasurementPolicyVersion = model.MeasurementPolicyVersion;
                existing.RawObjectCount = model.RawObjectCount;
                existing.MappedObjectCount = model.MappedObjectCount;
                existing.RawMemberCount = model.RawMemberCount;
                existing.MappedMemberCount = model.MappedMemberCount;
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
               entity.RunId != model.RunId ||
               entity.CollectionStatus != model.CollectionStatus ||
               entity.CollectionReason != model.CollectionReason ||
               entity.SuccessfulCollector != model.SuccessfulCollector ||
               entity.CollectionMetadataJson != model.CollectionMetadataJson ||
               entity.HasUsableCoverage != model.HasUsableCoverage ||
               entity.LineCountsAvailable != model.LineCountsAvailable ||
               entity.BranchCountsAvailable != model.BranchCountsAvailable ||
               entity.MeasurementPolicyVersion != model.MeasurementPolicyVersion ||
               entity.RawObjectCount != model.RawObjectCount ||
               entity.MappedObjectCount != model.MappedObjectCount ||
               entity.RawMemberCount != model.RawMemberCount ||
               entity.MappedMemberCount != model.MappedMemberCount ||
               entity.LinesCovered != model.LinesCovered ||
               entity.LinesValid != model.LinesValid ||
               entity.BranchesCovered != model.BranchesCovered ||
               entity.BranchesValid != model.BranchesValid;
    }

    private Task<CoverageReportEntity?> FindExistingAsync(CoverageReportModel model, int projectId)
    {
        return string.IsNullOrWhiteSpace(model.RunId)
            ? _context.CoverageReports.FirstOrDefaultAsync(x =>
                x.ProjectId == projectId && x.RunId == string.Empty && x.Timestamp == model.Timestamp)
            : _context.CoverageReports.FirstOrDefaultAsync(x =>
                x.ProjectId == projectId && x.RunId == model.RunId);
    }

    private static double SanitizeDouble(double value)
    {
        return double.IsFinite(value) ? value : 0.0;
    }
}
