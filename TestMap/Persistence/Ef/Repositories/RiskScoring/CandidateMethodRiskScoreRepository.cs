using Microsoft.EntityFrameworkCore;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef.Mapping.RiskScoring;

namespace TestMap.Persistence.Ef.Repositories.RiskScoring;

public class CandidateMethodRiskScoreRepository
{
    private readonly TestMapDbContext _context;

    public CandidateMethodRiskScoreRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<MethodRiskScore?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.CandidateMethodRiskScores
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<List<MethodRiskScore>> GetByCandidateMethodIdAsync(
        int candidateMethodId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.CandidateMethodRiskScores
            .Where(x => x.CandidateMethodId == candidateMethodId)
            .OrderByDescending(x => x.RiskScore)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<List<MethodRiskScore>> GetByMemberIdAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.CandidateMethodRiskScores
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<int> InsertAsync(MethodRiskScore score, CancellationToken cancellationToken = default)
    {
        var entity = score.ToEntity();
        _context.CandidateMethodRiskScores.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task BulkInsertAsync(
        IReadOnlyList<MethodRiskScore> scores,
        CancellationToken cancellationToken = default)
    {
        var entities = scores.Select(x => x.ToEntity()).ToList();
        _context.CandidateMethodRiskScores.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
