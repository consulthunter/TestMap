using LibGit2Sharp;
using TestMap.App;

namespace TestMap.Services.TestGeneration.Workspace;

public sealed class RollbackWorkspaceService : IGenerationWorkspaceService
{
    private readonly ProjectContext _context;

    public RollbackWorkspaceService(ProjectContext context)
    {
        _context = context;
    }

    public Task EnsureWorkspaceReadyAsync(CancellationToken cancellationToken = default)
    {
        EnsureCleanWorkingTree();
        return Task.CompletedTask;
    }

    public Task RollbackChangesAsync(CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_context.Project.DirectoryPath);
        var headCommit = repo.Head.Tip ??
                         throw new InvalidOperationException("Repository has no HEAD commit to reset to.");
        repo.Reset(ResetMode.Hard, headCommit);
        DeleteUntrackedFiles(repo);
        _context.Project.Logger?.Debug("Repository rolled back");
        return Task.CompletedTask;
    }

    public Task PersistAcceptedChangesAsync(string message, CancellationToken cancellationToken = default)
    {
        _context.Project.Logger?.Debug("Rollback workspace ignores persist request: {Message}", message);
        return Task.CompletedTask;
    }

    private void EnsureCleanWorkingTree()
    {
        using var repo = new Repository(_context.Project.DirectoryPath);
        var status = repo.RetrieveStatus(new StatusOptions());
        if (status.IsDirty)
            throw new InvalidOperationException(
                "Experiment workspace requires a clean git working tree because failed attempts are rolled back.");
    }

    private static void DeleteUntrackedFiles(Repository repo)
    {
        var status = repo.RetrieveStatus(new StatusOptions());
        foreach (var untracked in status.Untracked)
        {
            var path = Path.Combine(repo.Info.WorkingDirectory, untracked.FilePath);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}