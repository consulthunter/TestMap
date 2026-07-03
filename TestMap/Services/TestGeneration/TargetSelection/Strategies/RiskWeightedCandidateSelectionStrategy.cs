using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Mapping.Code;
using TestMap.Services.RiskScoring;

namespace TestMap.Services.TestGeneration.TargetSelection.Strategies;

public sealed class RiskWeightedCandidateSelectionStrategy : ICandidateSelectionStrategy
{
    private readonly TestMapDbContext _dbContext;
    private readonly IRiskScoringService _riskScoringService;

    public RiskWeightedCandidateSelectionStrategy(
        TestMapDbContext dbContext,
        IRiskScoringService riskScoringService)
    {
        _dbContext = dbContext;
        _riskScoringService = riskScoringService;
    }

    public TargetSelectionStrategy Strategy => TargetSelectionStrategy.RiskWeighted;

    public async Task<IReadOnlyList<CandidateMethod>> SelectAsync(
        CandidateSelectionContext context,
        IReadOnlyList<CandidateSelectionRow> candidatePool,
        CancellationToken cancellationToken = default)
    {
        var memberIds = candidatePool.Select(x => x.Id).ToList();
        var members = await _dbContext.Members
            .Where(x => memberIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var memberModels = members
            .Select(x => x.ToDomain())
            .ToList();

        var scores = await _riskScoringService.ScoreAsync(
            new RiskScoringRequest(memberModels, context.TargetSelection),
            cancellationToken);
        var scoresByMemberId = scores.ToDictionary(x => x.MemberId);

        return candidatePool
            .OrderByDescending(x => scoresByMemberId.TryGetValue(x.Id, out var score) ? score.RiskScore : 0.0)
            .ThenBy(x => x.LineRate)
            .ThenByDescending(x => x.Complexity)
            .Select(row => CandidateMethodFactory.Create(
                row,
                context.SelectionTime,
                scoresByMemberId.TryGetValue(row.Id, out var riskScore) ? riskScore : null))
            .ToList();
    }
}
