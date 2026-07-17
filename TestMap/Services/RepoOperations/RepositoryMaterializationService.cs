using LibGit2Sharp;
using TestMap.App;
using TestMap.Models.Targets;

namespace TestMap.Services.RepoOperations;

public interface IRepositoryMaterializationService
{
    Task<MaterializedRevision> MaterializeAsync(CancellationToken cancellationToken = default);
}

public sealed class RepositoryMaterializationService(
    ProjectContext context,
    RepositoryUrlService urls,
    IRevisionWorkspaceLock workspaceLock) : IRepositoryMaterializationService
{
    public async Task<MaterializedRevision> MaterializeAsync(CancellationToken cancellationToken = default)
    {
        var target = context.RepositoryTarget ?? throw new InvalidOperationException("Pinned materialization requires a repository target.");
        var current = context.MaterializedRevision ?? throw new InvalidOperationException("Pinned materialization requires initialized revision paths and provenance.");
        await using var handle = await workspaceLock.AcquireAsync(current.Paths.WorkspacePath, cancellationToken);
        try
        {
            await Task.Run(() => Materialize(target, current.Paths.WorkspacePath, cancellationToken), cancellationToken);
            using var repository = new Repository(current.Paths.WorkspacePath);
            var resolved = repository.Head.Tip?.Sha?.ToLowerInvariant();
            if (!string.Equals(resolved, target.Commit, StringComparison.Ordinal))
                throw new RepositoryMaterializationException(MaterializationStatus.CommitUnavailable, "Materialized revision does not equal the requested commit.");
            if (repository.RetrieveStatus().IsDirty)
                throw new RepositoryMaterializationException(MaterializationStatus.WorkspaceDirty, "Materialized workspace is not clean.");

            var origin = repository.Network.Remotes["origin"]?.Url;
            var revision = current with
            {
                ResolvedCommit = resolved,
                OriginUrl = urls.RedactCredentials(origin ?? target.Url),
                MaterializedAtUtc = DateTimeOffset.UtcNow,
                Status = MaterializationStatus.Available
            };
            context.MaterializedRevision = revision;
            context.RepoPath = revision.Paths.WorkspacePath;
            context.CurrentCommit = resolved;
            context.Project.Commit = resolved;
            context.Project.LastAnalyzedCommit = resolved;
            return revision;
        }
        catch (RepositoryMaterializationException) { throw; }
        catch (LibGit2SharpException exception)
        {
            throw Classify(exception);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RepositoryMaterializationException(MaterializationStatus.Failed, "Repository materialization failed.", exception);
        }
    }

    private void Materialize(RepositoryTarget target, string workspacePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(workspacePath) && !Repository.IsValid(workspacePath))
            throw new RepositoryMaterializationException(MaterializationStatus.InvalidWorkspace, "Existing workspace is not a valid Git repository.");

        if (!Repository.IsValid(workspacePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(workspacePath)!);
            Repository.Clone(target.Url, workspacePath);
        }

        using var repository = new Repository(workspacePath);
        var origin = repository.Network.Remotes["origin"]?.Url;
        if (origin is null || !urls.Equivalent(origin, target.Url))
            throw new RepositoryMaterializationException(MaterializationStatus.WrongOrigin, "Existing workspace origin does not match the target repository.");
        RepositoryManagedArtifactExclusions.Ensure(repository);
        if (repository.RetrieveStatus().IsDirty)
            throw new RepositoryMaterializationException(MaterializationStatus.WorkspaceDirty, "Existing workspace has uncommitted changes.");

        var commit = repository.Lookup<Commit>(target.Commit);
        if (commit is null)
        {
            Commands.Fetch(repository, "origin", ["+refs/heads/*:refs/remotes/origin/*", "+refs/tags/*:refs/tags/*"], new FetchOptions(), null);
            commit = repository.Lookup<Commit>(target.Commit);
        }
        if (commit is null)
            throw new RepositoryMaterializationException(MaterializationStatus.CommitUnavailable, "Requested commit is unavailable.");

        Commands.Checkout(repository, commit, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
        repository.Reset(ResetMode.Hard, commit);
    }

    private static RepositoryMaterializationException Classify(LibGit2SharpException exception)
    {
        var message = exception.Message.ToLowerInvariant();
        var status = message.Contains("authentication") || message.Contains("credentials")
            ? MaterializationStatus.AuthenticationFailed
            : message.Contains("not found") || message.Contains("repository")
                ? MaterializationStatus.RepositoryUnavailable
                : MaterializationStatus.Failed;
        return new RepositoryMaterializationException(status, "Repository materialization failed.", exception);
    }
}
