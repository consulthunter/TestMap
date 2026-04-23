using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;

namespace TestMap.Services.RiskScoring;

public class ComplexityRiskFactorProvider : IRiskFactorProvider
{
    public RiskFactorKind Factor => RiskFactorKind.Complexity;

    public Task<RiskFactorScore> ScoreAsync(MemberModel candidateMember, CancellationToken cancellationToken = default)
        => Task.FromResult(new RiskFactorScore(Factor, 0.0, "Complexity scoring is not implemented yet."));
}
