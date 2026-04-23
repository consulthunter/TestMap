using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;

namespace TestMap.Services.RiskScoring;

public class ChurnRiskFactorProvider : IRiskFactorProvider
{
    public RiskFactorKind Factor => RiskFactorKind.Churn;

    public Task<RiskFactorScore> ScoreAsync(MemberModel candidateMember, CancellationToken cancellationToken = default)
        => Task.FromResult(new RiskFactorScore(Factor, 0.0, "Churn scoring is not implemented yet."));
}
