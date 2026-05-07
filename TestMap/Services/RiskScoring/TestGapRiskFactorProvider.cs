using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;

namespace TestMap.Services.RiskScoring;

public class TestGapRiskFactorProvider(TestMapDbContext dbContext) : IRiskFactorProvider
{
    public RiskFactorKind Factor => RiskFactorKind.TestGap;

    public async Task<RiskFactorScore> ScoreAsync(MemberModel candidateMember,
        CancellationToken cancellationToken = default)
    {
        var testRelationships = await dbContext.MemberRelationships
            .CountAsync(
                x => x.TargetId == candidateMember.Id &&
                     (x.RelationshipType == "tests" || x.RelationshipType == "covers"),
                cancellationToken);
        var testInvocations = await (
                from invocation in dbContext.Invocations
                join member in dbContext.Members on invocation.MemberId equals member.Id
                where invocation.InvokedMemberId == candidateMember.Id && member.IsTestMember
                select invocation.Id)
            .CountAsync(cancellationToken);
        var latestCoverage = await dbContext.MemberCoverages
            .Where(x => x.MemberId == candidateMember.Id)
            .OrderByDescending(x => x.CoverageReportId)
            .ThenByDescending(x => x.Id)
            .Select(x => x.LineRate)
            .FirstOrDefaultAsync(cancellationToken);

        var hasDirectTestSignal = testRelationships + testInvocations > 0;
        var score = hasDirectTestSignal
            ? Math.Max(0.0, 1.0 - latestCoverage) * 0.35
            : Math.Max(0.65, 1.0 - latestCoverage);

        return new RiskFactorScore(
            Factor,
            score,
            $"direct test signals={testRelationships + testInvocations}, coverage={latestCoverage:P1}");
    }
}