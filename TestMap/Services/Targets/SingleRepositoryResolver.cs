using TestMap.Models.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.Targets;

public sealed class SingleRepositoryResolver(
    IRepositoryResolutionClient client,
    TargetIdentityService identity,
    SingleRepositoryResolutionSanitizer sanitizer,
    SingleRepositoryResolutionValidator validator) : ISingleRepositoryResolver
{
    public async Task<SingleRepositoryResolution> ResolveAsync(
        SingleRepositoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var attemptedPasses = 1;
        try
        {
            var first = await ResolvePassAsync(request, cancellationToken);
            if (first.Terminal is not null)
                return Valid(first.Terminal with { RequestedAtUtc = requestedAt, CompletedAtUtc = DateTimeOffset.UtcNow });
            if (first.Consistent)
                return Valid(Success(request, first, requestedAt, 1));

            attemptedPasses = 2;
            var second = await ResolvePassAsync(request, cancellationToken);
            if (second.Terminal is not null)
                return Valid(second.Terminal with { RequestedAtUtc = requestedAt, CompletedAtUtc = DateTimeOffset.UtcNow, ResolutionPasses = 2 });
            if (second.Consistent)
                return Valid(Success(request, second, requestedAt, 2));
            return Valid(Failure(
                request, requestedAt, SingleRepositoryResolutionStatus.InconsistentResolution,
                "inconsistent_resolution", "Repository identity or default branch changed during both resolution passes.", 2));
        }
        catch (OperationCanceledException) { throw; }
        catch (RepositoryResolutionProviderException exception)
        {
            var (status, reason) = Map(exception.FailureKind);
            return Valid(Failure(
                request, requestedAt, status, reason, exception.Message, attemptedPasses,
                status == SingleRepositoryResolutionStatus.RateLimited ? exception.RetryAfterUtc?.ToUniversalTime() : null));
        }
        catch
        {
            return Valid(Failure(
                request, requestedAt, SingleRepositoryResolutionStatus.ResolutionFailed,
                "resolution_failed", "Repository resolution failed.", attemptedPasses));
        }
    }

    private async Task<ResolutionPass> ResolvePassAsync(
        SingleRepositoryRequest request,
        CancellationToken cancellationToken)
    {
        var parts = request.Repository.Split('/');
        var metadata = await client.GetRepositoryAsync(parts[0], parts[1], cancellationToken);
        if (!string.Equals(metadata.FullName, request.Repository, StringComparison.OrdinalIgnoreCase))
            return Terminal(SingleRepositoryResolutionStatus.RepositoryIdentityMismatch,
                "repository_identity_mismatch", "Provider repository identity differs from the requested repository.");
        if (string.IsNullOrWhiteSpace(metadata.DefaultBranch))
            return Terminal(SingleRepositoryResolutionStatus.EmptyRepository,
                "empty_repository", "Repository has no resolvable default branch.");

        var branch = await client.GetBranchAsync(parts[0], parts[1], metadata.DefaultBranch, cancellationToken);
        if (!string.Equals(branch.Name, metadata.DefaultBranch, StringComparison.Ordinal))
            return Terminal(SingleRepositoryResolutionStatus.DefaultBranchUnavailable,
                "default_branch_mismatch", "Provider returned a different branch than the named default branch.");
        if (!identity.TryNormalizeCommit(branch.CommitSha, out var branchCommit))
            return Terminal(SingleRepositoryResolutionStatus.CommitUnavailable,
                "invalid_branch_commit", "Default branch did not resolve to a full commit SHA.");
        var commit = await client.GetCommitAsync(parts[0], parts[1], branchCommit, cancellationToken);
        if (!identity.TryNormalizeCommit(commit.Sha, out var commitSha) || commitSha != branchCommit)
            return Terminal(SingleRepositoryResolutionStatus.CommitMismatch,
                "commit_mismatch", "Branch head and commit object identities differ.");
        var confirmation = await client.GetRepositoryAsync(parts[0], parts[1], cancellationToken);
        var consistent = string.Equals(confirmation.FullName, metadata.FullName, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(confirmation.DefaultBranch, metadata.DefaultBranch, StringComparison.Ordinal);
        return new ResolutionPass(
            consistent,
            metadata.FullName.ToLowerInvariant(),
            metadata.DefaultBranch,
            commitSha,
            null);

        ResolutionPass Terminal(SingleRepositoryResolutionStatus status, string reason, string summary) => new(
            false, null, null, null,
            Failure(request, DateTimeOffset.UtcNow, status, reason, summary, 1));
    }

    private SingleRepositoryResolution Success(
        SingleRepositoryRequest request,
        ResolutionPass pass,
        DateTimeOffset requestedAt,
        int passes) => new(
        1, requestedAt, DateTimeOffset.UtcNow, request.RequestedUrl, request.RequestedUrlSha256,
        request.Repository, request.CanonicalUrl, request.Provider, request.PolicyName,
        request.PolicyVersion, request.AuthenticationMode, SingleRepositoryResolutionStatus.Resolved,
        pass.Repository, pass.DefaultBranch, pass.Commit, passes, null, null,
        sanitizer.Sanitize("The default-branch head was resolved to an exact commit."));

    private SingleRepositoryResolution Failure(
        SingleRepositoryRequest request,
        DateTimeOffset requestedAt,
        SingleRepositoryResolutionStatus status,
        string reason,
        string summary,
        int passes,
        DateTimeOffset? retryAfterUtc = null) => new(
        1, requestedAt.ToUniversalTime(), DateTimeOffset.UtcNow, request.RequestedUrl,
        request.RequestedUrlSha256, request.Repository, request.CanonicalUrl, request.Provider,
        request.PolicyName, request.PolicyVersion, request.AuthenticationMode, status,
        null, null, null, passes, reason, retryAfterUtc,
        sanitizer.Sanitize(summary));

    private SingleRepositoryResolution Valid(SingleRepositoryResolution resolution)
    {
        validator.Validate(resolution);
        return resolution;
    }

    private static (SingleRepositoryResolutionStatus Status, string Reason) Map(
        RepositoryResolutionFailureKind kind) => kind switch
    {
        RepositoryResolutionFailureKind.RepositoryUnavailable => (SingleRepositoryResolutionStatus.RepositoryUnavailable, "repository_unavailable"),
        RepositoryResolutionFailureKind.AuthenticationFailed => (SingleRepositoryResolutionStatus.AuthenticationFailed, "authentication_failed"),
        RepositoryResolutionFailureKind.AuthorizationFailed => (SingleRepositoryResolutionStatus.AuthorizationFailed, "authorization_failed"),
        RepositoryResolutionFailureKind.RateLimited => (SingleRepositoryResolutionStatus.RateLimited, "rate_limited"),
        RepositoryResolutionFailureKind.EmptyRepository => (SingleRepositoryResolutionStatus.EmptyRepository, "empty_repository"),
        RepositoryResolutionFailureKind.DefaultBranchUnavailable => (SingleRepositoryResolutionStatus.DefaultBranchUnavailable, "default_branch_unavailable"),
        RepositoryResolutionFailureKind.CommitUnavailable => (SingleRepositoryResolutionStatus.CommitUnavailable, "commit_unavailable"),
        RepositoryResolutionFailureKind.ServiceUnavailable => (SingleRepositoryResolutionStatus.ServiceUnavailable, "service_unavailable"),
        _ => (SingleRepositoryResolutionStatus.ResolutionFailed, "resolution_failed")
    };

    private sealed record ResolutionPass(
        bool Consistent,
        string? Repository,
        string? DefaultBranch,
        string? Commit,
        SingleRepositoryResolution? Terminal);
}
