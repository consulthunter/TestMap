using Octokit;
using TestMap.Services.ProjectDiscovery.Contracts;

namespace TestMap.Services.ProjectDiscovery;

public sealed class GitHubProjectTreeClient : IProjectTreeClient
{
    private readonly GitHubClient _client;
    private readonly GitHubProjectCheckFailureClassifier _failureClassifier;

    public GitHubProjectTreeClient(string? token, GitHubProjectCheckFailureClassifier failureClassifier)
    {
        _failureClassifier = failureClassifier;
        _client = new GitHubClient(new ProductHeaderValue("TestMap"));
        if (!string.IsNullOrWhiteSpace(token)) _client.Credentials = new Credentials(token);
    }

    public async Task<ProjectRepositoryInfo> GetRepositoryAsync(
        string owner, string repository, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _client.Repository.Get(owner, repository);
            cancellationToken.ThrowIfCancellationRequested();
            return new ProjectRepositoryInfo(result.FullName);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw _failureClassifier.Classify(exception, ProjectTreeFailureKind.RepositoryUnavailable);
        }
    }

    public async Task<ProjectCommitInfo> GetCommitAsync(
        string owner, string repository, string commit, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _client.Git.Commit.Get(owner, repository, commit);
            cancellationToken.ThrowIfCancellationRequested();
            return new ProjectCommitInfo(result.Sha, result.Tree.Sha);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw _failureClassifier.Classify(exception, ProjectTreeFailureKind.CommitUnavailable);
        }
    }

    public async Task<ProjectTreeResult> GetTreeAsync(
        string owner, string repository, string treeSha, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _client.Git.Tree.GetRecursive(owner, repository, treeSha);
            cancellationToken.ThrowIfCancellationRequested();
            return new ProjectTreeResult(
                result.Tree.Select(entry => new ProjectTreeEntry(entry.Path, entry.Type.StringValue)).ToArray(),
                result.Truncated);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw _failureClassifier.Classify(exception, ProjectTreeFailureKind.TreeUnavailable);
        }
    }
}
