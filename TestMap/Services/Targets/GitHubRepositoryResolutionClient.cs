using Octokit;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.Targets;

public sealed class GitHubRepositoryResolutionClient : IRepositoryResolutionClient
{
    private readonly GitHubClient _client;
    private readonly GitHubRepositoryResolutionFailureClassifier _classifier;

    public GitHubRepositoryResolutionClient(
        string? token,
        GitHubRepositoryResolutionFailureClassifier classifier)
    {
        _classifier = classifier;
        _client = new GitHubClient(new ProductHeaderValue("TestMap"));
        if (!string.IsNullOrWhiteSpace(token)) _client.Credentials = new Credentials(token);
    }

    public async Task<RepositoryResolutionMetadata> GetRepositoryAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _client.Repository.Get(owner, repository);
            cancellationToken.ThrowIfCancellationRequested();
            return new RepositoryResolutionMetadata(result.FullName, result.DefaultBranch);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw _classifier.Classify(exception, RepositoryResolutionFailureKind.RepositoryUnavailable);
        }
    }

    public async Task<RepositoryResolutionBranch> GetBranchAsync(
        string owner,
        string repository,
        string branch,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _client.Repository.Branch.Get(owner, repository, branch);
            cancellationToken.ThrowIfCancellationRequested();
            return new RepositoryResolutionBranch(result.Name, result.Commit.Sha);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw _classifier.Classify(exception, RepositoryResolutionFailureKind.DefaultBranchUnavailable);
        }
    }

    public async Task<RepositoryResolutionCommit> GetCommitAsync(
        string owner,
        string repository,
        string commit,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _client.Git.Commit.Get(owner, repository, commit);
            cancellationToken.ThrowIfCancellationRequested();
            return new RepositoryResolutionCommit(result.Sha);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw _classifier.Classify(exception, RepositoryResolutionFailureKind.CommitUnavailable);
        }
    }
}
