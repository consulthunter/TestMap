using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public sealed class CandidateCohortRepository
{
    private readonly TestMapDbContext _context;

    public CandidateCohortRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<CandidateCohort?> GetByKeyAsync(
        int projectId,
        string cohortKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.CandidateCohorts
            .AsNoTracking()
            .Include(x => x.Members)
            .FirstOrDefaultAsync(
                x => x.ProjectId == projectId && x.CohortKey == cohortKey,
                cancellationToken);

        return entity == null ? null : ToDomain(entity);
    }

    public async Task<CandidateCohort> InsertAsync(
        CandidateCohort cohort,
        CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(cohort);
        _context.CandidateCohorts.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDomain(entity);
    }

    private static CandidateCohort ToDomain(CandidateCohortEntity entity)
    {
        return new CandidateCohort
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            CohortKey = entity.CohortKey,
            RepositoryIdentity = entity.RepositoryIdentity,
            CommitHash = entity.CommitHash,
            Objective = entity.Objective,
            SelectionStrategy = entity.SelectionStrategy,
            SelectionConfigurationHash = entity.SelectionConfigurationHash,
            CandidateLimit = entity.CandidateLimit,
            RandomSeed = entity.RandomSeed,
            CreatedAt = entity.CreatedAt,
            Members = entity.Members
                .OrderBy(x => x.Ordinal)
                .Select(x => new CandidateCohortMember
                {
                    Id = x.Id,
                    CandidateCohortId = x.CandidateCohortId,
                    Ordinal = x.Ordinal,
                    SourceMemberId = x.SourceMemberId,
                    SourceMethodName = x.SourceMethodName,
                    SourceMethodSignature = x.SourceMethodSignature,
                    SourceFilePath = x.SourceFilePath,
                    ContainingType = x.ContainingType,
                    SourceContentHash = x.SourceContentHash,
                    CandidateSnapshotJson = x.CandidateSnapshotJson
                })
                .ToList()
        };
    }

    private static CandidateCohortEntity ToEntity(CandidateCohort cohort)
    {
        return new CandidateCohortEntity
        {
            Id = cohort.Id,
            ProjectId = cohort.ProjectId,
            CohortKey = cohort.CohortKey,
            RepositoryIdentity = cohort.RepositoryIdentity,
            CommitHash = cohort.CommitHash,
            Objective = cohort.Objective,
            SelectionStrategy = cohort.SelectionStrategy,
            SelectionConfigurationHash = cohort.SelectionConfigurationHash,
            CandidateLimit = cohort.CandidateLimit,
            RandomSeed = cohort.RandomSeed,
            CreatedAt = cohort.CreatedAt,
            Members = cohort.Members.Select(x => new CandidateCohortMemberEntity
            {
                Id = x.Id,
                Ordinal = x.Ordinal,
                SourceMemberId = x.SourceMemberId,
                SourceMethodName = x.SourceMethodName,
                SourceMethodSignature = x.SourceMethodSignature,
                SourceFilePath = x.SourceFilePath,
                ContainingType = x.ContainingType,
                SourceContentHash = x.SourceContentHash,
                CandidateSnapshotJson = x.CandidateSnapshotJson
            }).ToList()
        };
    }
}
