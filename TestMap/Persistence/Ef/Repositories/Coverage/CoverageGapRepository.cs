using Microsoft.EntityFrameworkCore;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.Persistence.Ef.Repositories.Coverage;

public class CoverageGapRepository
{
    private readonly TestMapDbContext _context;

    public CoverageGapRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task ReplaceForMemberAsync(
        int memberId,
        int coverageReportId,
        IReadOnlyCollection<CoverageGapModel> gaps)
    {
        var existing = await _context.CoverageGaps
            .Where(x => x.MemberId == memberId && x.CoverageReportId == coverageReportId)
            .ToListAsync();

        if (existing.Count > 0) _context.CoverageGaps.RemoveRange(existing);

        if (gaps.Count > 0) _context.CoverageGaps.AddRange(gaps.Select(x => x.ToEntity()));

        await _context.SaveChangesAsync();
    }

    public async Task<List<CoverageGapModel>> GetByMemberAsync(int memberId, int coverageReportId)
    {
        var entities = await _context.CoverageGaps
            .Where(x => x.MemberId == memberId && x.CoverageReportId == coverageReportId)
            .OrderBy(x => x.LineNumber)
            .ThenBy(x => x.GapKind)
            .ToListAsync();

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<List<CoverageGapModel>> GetLatestByMemberAsync(int memberId)
    {
        var latestReportId = await (
                from gap in _context.CoverageGaps
                join report in _context.CoverageReports on gap.CoverageReportId equals report.Id
                where gap.MemberId == memberId
                orderby report.Timestamp descending, report.CreatedAt descending, gap.Id descending
                select (int?)gap.CoverageReportId)
            .FirstOrDefaultAsync();

        if (latestReportId == null) return [];

        return await GetByMemberAsync(memberId, latestReportId.Value);
    }
}