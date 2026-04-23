using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;

namespace TestMap.Services.RiskScoring;

public class TestGapRiskFactorProvider : IRiskFactorProvider
{
    public RiskFactorKind Factor => RiskFactorKind.TestGap;

    public Task<RiskFactorScore> ScoreAsync(MemberModel candidateMember, CancellationToken cancellationToken = default)
        => Task.FromResult(new RiskFactorScore(Factor, 0.0, "Test gap scoring is not implemented yet."));
}
