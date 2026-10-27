using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Services.Experiment.Execution;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public sealed class CandidateBaselineRepository(TestMapDbContext context) : ICandidateBaselineStore
{
    public async Task<CandidateBaselineRecord?> GetAsync(string stableKey, CancellationToken cancellationToken = default)
    {
        var entity = await context.CandidateBaselines
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StableKey == stableKey, cancellationToken);
        return entity == null
            ? null
            : new CandidateBaselineRecord(
                entity.StableKey,
                entity.ExperimentRunId,
                entity.CandidateMethodId,
                entity.MemberId,
                entity.TestRunId,
                entity.Usable,
                entity.FailureReason);
    }

    public async Task SaveAsync(CandidateBaselineRecord record, CancellationToken cancellationToken = default)
    {
        context.CandidateBaselines.Add(new CandidateBaselineEntity
        {
            StableKey = record.StableKey,
            ExperimentRunId = record.ExperimentRunId,
            CandidateMethodId = record.CandidateMethodId,
            MemberId = record.MemberId,
            TestRunId = record.TestRunId,
            Usable = record.Usable,
            FailureReason = record.FailureReason,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
