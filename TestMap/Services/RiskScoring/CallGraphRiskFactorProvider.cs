using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;

namespace TestMap.Services.RiskScoring;

public class CallGraphRiskFactorProvider(TestMapDbContext dbContext) : IRiskFactorProvider
{
    public RiskFactorKind Factor => RiskFactorKind.CallGraph;

    public async Task<RiskFactorScore> ScoreAsync(MemberModel candidateMember,
        CancellationToken cancellationToken = default)
    {
        var incomingRelationships = await dbContext.MemberRelationships
            .CountAsync(x => x.TargetId == candidateMember.Id, cancellationToken);
        var outgoingRelationships = await dbContext.MemberRelationships
            .CountAsync(x => x.SourceId == candidateMember.Id, cancellationToken);
        var incomingInvocations = await dbContext.Invocations
            .CountAsync(x => x.InvokedMemberId == candidateMember.Id, cancellationToken);
        var outgoingInvocations = await dbContext.Invocations
            .CountAsync(x => x.MemberId == candidateMember.Id && x.InvokedMemberId != null, cancellationToken);

        var fanIn = incomingRelationships + incomingInvocations;
        var fanOut = outgoingRelationships + outgoingInvocations;
        var fanInScore = Math.Min(1.0, fanIn / 20.0);
        var fanOutScore = Math.Min(1.0, fanOut / 20.0);
        var score = fanInScore * 0.65 + fanOutScore * 0.35;

        return new RiskFactorScore(
            Factor,
            score,
            $"fan-in={fanIn}, fan-out={fanOut}");
    }
}