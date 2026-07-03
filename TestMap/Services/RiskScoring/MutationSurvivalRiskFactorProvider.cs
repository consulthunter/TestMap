using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;

namespace TestMap.Services.RiskScoring;

public class MutationSurvivalRiskFactorProvider(TestMapDbContext dbContext) : IRiskFactorProvider
{
    public RiskFactorKind Factor => RiskFactorKind.MutationSurvival;

    public async Task<RiskFactorScore> ScoreAsync(MemberModel candidateMember,
        CancellationToken cancellationToken = default)
    {
        var mutants = await dbContext.Mutants
            .Where(x => x.MemberId == candidateMember.Id)
            .Select(x => x.Status)
            .ToListAsync(cancellationToken);

        if (mutants.Count == 0) return new RiskFactorScore(Factor, 0.0, "No mapped mutants are available.");

        var riskyStatuses = mutants.Count(IsUndetectedStatus);
        var score = (double)riskyStatuses / mutants.Count;

        return new RiskFactorScore(
            Factor,
            score,
            $"undetected mutants={riskyStatuses}/{mutants.Count}");
    }

    private static bool IsUndetectedStatus(string status)
    {
        return status.Equals("Survived", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("NoCoverage", StringComparison.OrdinalIgnoreCase);
    }
}