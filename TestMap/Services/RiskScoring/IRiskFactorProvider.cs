using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;

namespace TestMap.Services.RiskScoring;

public interface IRiskFactorProvider
{
    RiskFactorKind Factor { get; }

    Task<RiskFactorScore> ScoreAsync(
        MemberModel candidateMember,
        CancellationToken cancellationToken = default);
}