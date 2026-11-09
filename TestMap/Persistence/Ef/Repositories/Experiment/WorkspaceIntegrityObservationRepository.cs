using TestMap.Models.Targets;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Repositories.Experiment;

public sealed class WorkspaceIntegrityObservationRepository(TestMapDbContext context)
{
    public async Task<int> InsertAsync(WorkspaceIntegrityObservation observation, CancellationToken cancellationToken = default)
    {
        var entity = new WorkspaceIntegrityObservationEntity
        {
            ProjectId = observation.ProjectId,
            ExperimentRunId = observation.ExperimentRunId,
            TargetId = observation.TargetId,
            ProducerLane = observation.ProducerLane,
            WorkItemStableKey = observation.WorkItemStableKey,
            AttemptNumber = observation.AttemptNumber,
            Checkpoint = observation.Checkpoint.ToString(),
            ExpectedCommit = observation.ExpectedCommit,
            ActualCommit = observation.ActualCommit,
            OriginMatches = observation.OriginMatches,
            WorkingTreeDirty = observation.WorkingTreeDirty,
            Status = observation.Status.ToString(),
            Details = observation.Details,
            ObservedAtUtc = observation.ObservedAtUtc.UtcDateTime
        };
        context.WorkspaceIntegrityObservations.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
