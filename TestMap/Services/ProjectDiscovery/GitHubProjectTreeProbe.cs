using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery.Contracts;

namespace TestMap.Services.ProjectDiscovery;

public sealed class GitHubProjectTreeProbe(
    IProjectTreeClient client,
    IProjectTestPresencePolicy policy,
    ProjectCheckSanitizer sanitizer) : IProjectCheckProbe
{
    public async Task<ProjectCheckObservation> CheckAsync(
        RepositoryTarget target,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        string? observedCommit = null;
        try
        {
            var parts = target.Repository.Split('/');
            if (parts.Length != 2)
                return Failure(ProjectCheckStatus.InvalidTarget, "invalid_target", "Target repository identity is invalid.");

            var repository = await client.GetRepositoryAsync(parts[0], parts[1], cancellationToken);
            if (!string.Equals(repository.FullName, target.Repository, StringComparison.OrdinalIgnoreCase))
                return Failure(ProjectCheckStatus.RepositoryIdentityMismatch, "repository_identity_mismatch", "Repository identity does not match the target.");

            var commit = await client.GetCommitAsync(parts[0], parts[1], target.Commit, cancellationToken);
            observedCommit = commit.Sha.ToLowerInvariant();
            if (!string.Equals(observedCommit, target.Commit, StringComparison.Ordinal))
                return Failure(ProjectCheckStatus.CommitMismatch, "commit_mismatch", "Resolved commit does not match the requested commit.");

            var tree = await client.GetTreeAsync(parts[0], parts[1], commit.TreeSha, cancellationToken);
            var evidence = policy.FindEvidence(tree.Entries.Select(entry => entry.Path));
            if (evidence is not null)
                return Observation(
                    ProjectCheckStatus.TestsDetected,
                    evidence.Category,
                    sanitizer.EvidencePath(evidence.Path),
                    !tree.Truncated,
                    null,
                    "Recognized test evidence was detected at the requested commit.");
            if (tree.Truncated)
                return Failure(
                    ProjectCheckStatus.TreeTruncated,
                    "recursive_tree_truncated",
                    "The returned tree was incomplete and contained no recognized positive evidence.",
                    treeComplete: false);
            return Observation(
                ProjectCheckStatus.NoTestsDetected, null, null, true, null,
                "No recognized test evidence was detected in the complete requested-commit tree.");
        }
        catch (ProjectTreeProviderException exception)
        {
            var (status, reason) = Map(exception.FailureKind);
            return Failure(status, reason, exception.Message);
        }

        ProjectCheckObservation Failure(
            ProjectCheckStatus status,
            string reason,
            string summary,
            bool? treeComplete = null) =>
            Observation(status, null, null, treeComplete, reason, summary);

        ProjectCheckObservation Observation(
            ProjectCheckStatus status,
            ProjectCheckEvidenceCategory? evidenceCategory,
            string? evidencePath,
            bool? treeComplete,
            string? reason,
            string summary) => new(
                target.TargetId,
                target.Repository,
                target.Commit,
                observedCommit,
                status,
                policy.Name,
                policy.Version,
                evidenceCategory,
                evidencePath,
                treeComplete,
                reason,
                sanitizer.Summary(summary),
                now);
    }

    private static (ProjectCheckStatus Status, string Reason) Map(ProjectTreeFailureKind kind) => kind switch
    {
        ProjectTreeFailureKind.RepositoryUnavailable => (ProjectCheckStatus.RepositoryUnavailable, "repository_unavailable"),
        ProjectTreeFailureKind.AuthenticationFailed => (ProjectCheckStatus.AuthenticationFailed, "authentication_failed"),
        ProjectTreeFailureKind.AuthorizationFailed => (ProjectCheckStatus.AuthorizationFailed, "authorization_failed"),
        ProjectTreeFailureKind.RateLimited => (ProjectCheckStatus.RateLimited, "rate_limited"),
        ProjectTreeFailureKind.CommitUnavailable => (ProjectCheckStatus.CommitUnavailable, "commit_unavailable"),
        ProjectTreeFailureKind.TreeUnavailable => (ProjectCheckStatus.TreeUnavailable, "tree_unavailable"),
        ProjectTreeFailureKind.ServiceUnavailable => (ProjectCheckStatus.ServiceUnavailable, "service_unavailable"),
        _ => (ProjectCheckStatus.CheckFailed, "check_failed")
    };
}
