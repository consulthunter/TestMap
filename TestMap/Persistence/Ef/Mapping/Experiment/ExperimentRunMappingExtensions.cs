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
            Name = $"Experiment_{entity.Id}",
            StartedAt = entity.StartTime,
            CompletedAt = entity.EndTime,
            ProjectId = entity.ProjectId,
            ConfigurationJson = entity.Configuration,
            CandidateLimit = entity.CandidateLimit,
            Status = entity.Status
        };
    }

    public static ExperimentRunEntity ToEntity(this ExperimentRun run)
    {
        return new ExperimentRunEntity
        {
            Id = run.Id,
            StartTime = run.StartedAt,
            EndTime = run.CompletedAt,
            ProjectId = run.ProjectId,
            Configuration = run.ConfigurationJson,
            CandidateLimit = run.CandidateLimit,
            Status = string.IsNullOrWhiteSpace(run.Status) ? "Completed" : run.Status
        };
    }
}