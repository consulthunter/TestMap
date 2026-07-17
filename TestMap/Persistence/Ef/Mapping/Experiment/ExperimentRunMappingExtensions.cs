using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class ExperimentRunMappingExtensions
{
    public static ExperimentRun ToDomain(this ExperimentRunEntity entity)
    {
        return new ExperimentRun
        {
            Id = entity.Id,
            RunUid = entity.RunUid,
            Name = $"Experiment_{entity.Id}",
            StartedAt = entity.StartTime,
            CompletedAt = entity.EndTime,
            ProjectId = entity.ProjectId,
            Objective = entity.Objective,
            CandidateSelectionStrategy = entity.CandidateSelectionStrategy,
            ConfigurationJson = entity.Configuration,
            ResultsFilePath = entity.ResultsFilePath,
            CandidateLimit = entity.CandidateLimit,
            Status = entity.Status,
            ExperimentSeriesId = entity.ExperimentSeriesId,
            CandidateCohortId = entity.CandidateCohortId,
            TargetId = entity.TargetId,
            RepositoryIdentity = entity.RepositoryIdentity,
            RequestedCommit = entity.RequestedCommit,
            ResolvedCommit = entity.ResolvedCommit,
            TargetManifestSha256 = entity.TargetManifestSha256,
            TargetSourceSha256 = entity.TargetSourceSha256,
            MaterializedAtUtc = entity.MaterializedAtUtc,
            WorkspaceIntegrityStatus = entity.WorkspaceIntegrityStatus,
            ProvenancePolicyVersion = entity.ProvenancePolicyVersion
        };
    }

    public static ExperimentRunEntity ToEntity(this ExperimentRun run)
    {
        return new ExperimentRunEntity
        {
            Id = run.Id,
            RunUid = string.IsNullOrWhiteSpace(run.RunUid) ? Guid.NewGuid().ToString("N") : run.RunUid,
            StartTime = run.StartedAt,
            EndTime = run.CompletedAt,
            ProjectId = run.ProjectId,
            Objective = run.Objective,
            CandidateSelectionStrategy = run.CandidateSelectionStrategy,
            Configuration = run.ConfigurationJson,
            ResultsFilePath = run.ResultsFilePath,
            CandidateLimit = run.CandidateLimit,
            Status = string.IsNullOrWhiteSpace(run.Status) ? "Completed" : run.Status,
            ExperimentSeriesId = run.ExperimentSeriesId,
            CandidateCohortId = run.CandidateCohortId,
            TargetId = run.TargetId,
            RepositoryIdentity = run.RepositoryIdentity,
            RequestedCommit = run.RequestedCommit,
            ResolvedCommit = run.ResolvedCommit,
            TargetManifestSha256 = run.TargetManifestSha256,
            TargetSourceSha256 = run.TargetSourceSha256,
            MaterializedAtUtc = run.MaterializedAtUtc,
            WorkspaceIntegrityStatus = run.WorkspaceIntegrityStatus,
            ProvenancePolicyVersion = run.ProvenancePolicyVersion
        };
    }
}
