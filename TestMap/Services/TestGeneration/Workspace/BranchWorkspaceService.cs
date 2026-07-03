using LibGit2Sharp;
using TestMap.App;

namespace TestMap.Services.TestGeneration.Workspace;

public sealed class BranchWorkspaceService : IGenerationWorkspaceService
{
    private readonly ProjectContext _context;
    private string? _branchName;

    public BranchWorkspaceService(ProjectContext context)
    {
        _context = context;
    }

    public Task EnsureWorkspaceReadyAsync(CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_context.Project.DirectoryPath);
        var status = repo.RetrieveStatus(new StatusOptions());
        if (status.IsDirty)
            throw new InvalidOperationException(
                "Regular generation requires a clean git working tree before creating a generation branch.");

        if (_branchName == null)
        {
            _branchName = $"testmap/generation/{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            var branch = repo.CreateBranch(_branchName);
            Commands.Checkout(repo, branch);
            _context.Project.Logger?.Information("Created generation branch {BranchName}", _branchName);
        }

        return Task.CompletedTask;
    }

    public Task RollbackChangesAsync(CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_context.Project.DirectoryPath);
        var headCommit = repo.Head.Tip ??
                         throw new InvalidOperationException("Repository has no HEAD commit to reset to.");
        repo.Reset(ResetMode.Hard, headCommit);
        DeleteUntrackedFiles(repo);
        _context.Project.Logger?.Debug("Rolled back failed generation attempt");
        return Task.CompletedTask;
    }

    public Task PersistAcceptedChangesAsync(string message, CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_context.Project.DirectoryPath);
        Commands.Stage(repo, "*");

        var status = repo.RetrieveStatus(new StatusOptions());
        if (!status.IsDirty) return Task.CompletedTask;

        var signature = BuildSignature(repo);
        repo.Commit(message, signature, signature);
        _context.Project.Logger?.Information("Committed accepted generation changes to {BranchName}",
            repo.Head.FriendlyName);
        return Task.CompletedTask;
    }

    private static Signature BuildSignature(Repository repo)
    {
        var name = repo.Config.Get<string>("user.name")?.Value;
        var email = repo.Config.Get<string>("user.email")?.Value;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            return new Signature("TestMap", "testmap@example.local", DateTimeOffset.UtcNow);

        return new Signature(name, email, DateTimeOffset.UtcNow);
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