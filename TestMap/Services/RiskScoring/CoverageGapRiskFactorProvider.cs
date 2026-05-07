using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;

namespace TestMap.Services.RiskScoring;

public class CoverageGapRiskFactorProvider(TestMapDbContext dbContext) : IRiskFactorProvider
{
    public RiskFactorKind Factor => RiskFactorKind.CoverageGap;

    public async Task<RiskFactorScore> ScoreAsync(MemberModel candidateMember,
        CancellationToken cancellationToken = default)
    {
        var latestCoverage = await dbContext.MemberCoverages
            .Where(x => x.MemberId == candidateMember.Id)
            .OrderByDescending(x => x.CoverageReportId)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.CoverageReportId,
                x.LineRate,
                x.BranchRate,
                x.LinesValid,
                x.LinesCovered,
                x.BranchesValid,
                x.BranchesCovered
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestCoverage == null) return new RiskFactorScore(Factor, 0.0, "No coverage data is available.");

        var gapCount = await dbContext.CoverageGaps
            .CountAsync(
                x => x.MemberId == candidateMember.Id &&
                     x.CoverageReportId == latestCoverage.CoverageReportId,
                cancellationToken);

        var lineGap = 1.0 - latestCoverage.LineRate;
        var branchGap = latestCoverage.BranchesValid > 0 ? 1.0 - latestCoverage.BranchRate : lineGap;
        var gapDensity = latestCoverage.LinesValid > 0
            ? Math.Min(1.0, (double)gapCount / latestCoverage.LinesValid)
            : 0.0;
        var score = lineGap * 0.60 + branchGap * 0.25 + gapDensity * 0.15;

        return new RiskFactorScore(
            Factor,
            score,
            $"line coverage={latestCoverage.LineRate:P1}, branch coverage={latestCoverage.BranchRate:P1}, gaps={gapCount}");
    }
}