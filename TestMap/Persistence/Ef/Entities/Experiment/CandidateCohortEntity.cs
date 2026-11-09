namespace TestMap.Persistence.Ef.Entities.Experiment;

public sealed class CandidateCohortEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string CohortKey { get; set; } = string.Empty;
    public string RepositoryIdentity { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string SelectionStrategy { get; set; } = string.Empty;
    public string SelectionConfigurationHash { get; set; } = string.Empty;
    public int CandidateLimit { get; set; }
    public int? RandomSeed { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<CandidateCohortMemberEntity> Members { get; set; } = [];
    public ICollection<ExperimentRunEntity> ExperimentRuns { get; set; } = [];
}

public sealed class CandidateCohortMemberEntity
{
    public int Id { get; set; }
    public int CandidateCohortId { get; set; }
    public int Ordinal { get; set; }
    public int SourceMemberId { get; set; }
    public string SourceMethodName { get; set; } = string.Empty;
    public string SourceMethodSignature { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public string ContainingType { get; set; } = string.Empty;
    public string SourceContentHash { get; set; } = string.Empty;
    public string CandidateSnapshotJson { get; set; } = string.Empty;

    public CandidateCohortEntity? CandidateCohort { get; set; }
    public ICollection<CandidateMethodEntity> CandidateMethods { get; set; } = [];
}
