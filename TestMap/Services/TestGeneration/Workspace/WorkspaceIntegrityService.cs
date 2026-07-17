using LibGit2Sharp;
using TestMap.App;
using TestMap.Models.Targets;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Services.RepoOperations;

namespace TestMap.Services.TestGeneration.Workspace;

public interface IWorkspaceIntegrityService
{
    Task<WorkspaceIntegrityObservation> EvaluateAsync(
        IntegrityCheckpoint checkpoint,
        bool allowExpectedChanges = false,
        int? experimentRunId = null,
        string? producerLane = null,
        string? workItemStableKey = null,
        int? attemptNumber = null,
        CancellationToken cancellationToken = default);
}

public sealed class WorkspaceIntegrityService(
    ProjectContext context,
    RepositoryUrlService urls,
    WorkspaceIntegrityObservationRepository? repository = null) : IWorkspaceIntegrityService
{
    public async Task<WorkspaceIntegrityObservation> EvaluateAsync(
        IntegrityCheckpoint checkpoint,
        bool allowExpectedChanges = false,
        int? experimentRunId = null,
        string? producerLane = null,
        string? workItemStableKey = null,
        int? attemptNumber = null,
        CancellationToken cancellationToken = default)
    {
        var revision = context.MaterializedRevision
            ?? throw new InvalidOperationException("Workspace integrity requires materialized revision provenance.");
        var expected = revision.ResolvedCommit
            ?? throw new InvalidOperationException("Workspace integrity requires a resolved base commit.");
        string? actual = null;
        var originMatches = false;
        bool? dirty = null;
        WorkspaceIntegrityStatus status;
        string details;

        try
        {
            if (!Repository.IsValid(revision.Paths.WorkspacePath))
            {
                status = WorkspaceIntegrityStatus.RepositoryInvalid;
                details = "Workspace is not a valid Git repository.";
            }
            else
            {
                using var git = new Repository(revision.Paths.WorkspacePath);
                actual = git.Head.Tip?.Sha?.ToLowerInvariant();
                var origin = git.Network.Remotes["origin"]?.Url;
                originMatches = origin is not null && context.RepositoryTarget is not null && urls.Equivalent(origin, context.RepositoryTarget.Url);
                dirty = git.RetrieveStatus().IsDirty;
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    status = WorkspaceIntegrityStatus.RevisionMismatch;
                    details = "Workspace HEAD differs from the pinned base commit.";
                }
                else if (!originMatches)
                {
                    status = WorkspaceIntegrityStatus.OriginMismatch;
                    details = "Workspace origin differs from the pinned target repository.";
                }
                else if (dirty == true && !allowExpectedChanges)
                {
                    status = WorkspaceIntegrityStatus.UnexpectedChanges;
                    details = "Workspace contains changes at a clean checkpoint.";
                }
                else if (dirty == true)
                {
                    status = WorkspaceIntegrityStatus.VerifiedExpectedChanges;
                    details = "Workspace remains on the pinned commit with expected attempt changes.";
                }
                else
                {
                    status = WorkspaceIntegrityStatus.VerifiedClean;
                    details = "Workspace is clean at the pinned commit and expected origin.";
                }
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            status = WorkspaceIntegrityStatus.StatusUnavailable;
            details = $"Workspace status could not be read ({exception.GetType().Name}).";
        }

        var observation = new WorkspaceIntegrityObservation(
            context.Project.DbId,
            experimentRunId,
            revision.TargetId,
            producerLane,
            workItemStableKey,
            attemptNumber,
            checkpoint,
            expected,
            actual,
            originMatches,
            dirty,
            status,
            details,
            DateTimeOffset.UtcNow);
        if (repository is not null && context.Project.DbId > 0)
            await repository.InsertAsync(observation, cancellationToken);
        return observation;
    }
}
