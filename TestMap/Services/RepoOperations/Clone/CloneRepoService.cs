/*
 * consulthunter
 * 2024-11-07
 * Clones the project from the remote
 * using the URL and LibGit2Sharp
 * CloneRepoService.cs
 */

using LibGit2Sharp;
using TestMap.Models;
using TestMap.App;

namespace TestMap.Services.RepoOperations.Clone;

public class CloneRepoService(ProjectContext context) : ICloneRepoService
{
    /// <summary>
    ///     Entry point into the service
    /// </summary>
    public virtual async Task CloneRepoAsync()
    {
        await Clone();
    }

    /// <summary>
    ///     Clones a repository using LibGit2Sharp
    ///     if it's not already cloned.
    /// </summary>
    /// <returns></returns>
    private Task Clone()
    {
        try
        {
            if (Repository.IsValid(context.Project.DirectoryPath))
            {
                using var existingRepo = new Repository(context.Project.DirectoryPath);
                CaptureRepositoryMetadata(existingRepo);

                context.Project.Logger?.Information(
                    $"Repository already exists at {context.Project.DirectoryPath}, skipping clone.");
                return Task.CompletedTask;
            }

            var parentDir = Directory.GetParent(context.Project.DirectoryPath);
            if (parentDir is { Exists: true })
            {
                context.Project.Logger?.Information($"Cloning repository: {context.Project.GitHubUrl}");

                Repository.Clone(context.Project.GitHubUrl, context.Project.DirectoryPath);

                using var repo = new Repository(context.Project.DirectoryPath);
                CaptureRepositoryMetadata(repo);

                context.Project.Logger?.Information($"Finished cloning repository: {context.Project.GitHubUrl}");
            }
            else
            {
                context.Project.Logger?.Error($"Parent directory {parentDir?.FullName} does not exist.");
            }
        }
        catch (Exception ex)
        {
            context.Project.Logger?.Error($"Failed to clone repository: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private void CaptureRepositoryMetadata(Repository repo)
    {
        var branch = repo.Head.FriendlyName;
        var commitSha = repo.Head.Tip?.Sha;

        context.Project.Branch = branch;
        context.Project.Commit = commitSha;
        context.Project.LastAnalyzedCommit = commitSha;
        context.CurrentCommit = commitSha;

        context.Project.Logger?.Information(
            "Repository metadata captured. Branch: {Branch}, Commit: {Commit}",
            branch,
            commitSha);
    }
}
