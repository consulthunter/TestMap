using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;

namespace TestMap.Services.RiskScoring;

public class ComplexityRiskFactorProvider(TestMapDbContext dbContext) : IRiskFactorProvider
{
    public RiskFactorKind Factor => RiskFactorKind.Complexity;

    public async Task<RiskFactorScore> ScoreAsync(MemberModel candidateMember,
        CancellationToken cancellationToken = default)
    {
        var metric = await dbContext.CodeMetrics
            .Where(x => x.EntityId == candidateMember.Id && x.EntityType == "member")
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (metric == null)
        {
            var fallbackLines = Math.Max(0,
                candidateMember.Location.EndLineNumber - candidateMember.Location.StartLineNumber + 1);
            var fallbackScore = Math.Min(1.0, fallbackLines / 120.0);
            return new RiskFactorScore(Factor, fallbackScore, $"no metric row; source lines={fallbackLines}");
        }

        var complexityScore = Math.Min(1.0, metric.CyclomaticComplexity / 25.0);
        var couplingScore = Math.Min(1.0, metric.ClassCoupling / 20.0);
        var sizeScore = Math.Min(1.0, metric.SourceLinesOfCode / 150.0);
        var maintainabilityRisk = metric.MaintainabilityIndex <= 0
            ? 0.0
            : 1.0 - Math.Min(1.0, metric.MaintainabilityIndex / 100.0);

        var score = complexityScore * 0.45 +
                    couplingScore * 0.20 +
                    sizeScore * 0.20 +
                    maintainabilityRisk * 0.15;

        return new RiskFactorScore(
            Factor,
            score,
            $"cc={metric.CyclomaticComplexity}, coupling={metric.ClassCoupling}, sloc={metric.SourceLinesOfCode}, mi={metric.MaintainabilityIndex}");
    }
}