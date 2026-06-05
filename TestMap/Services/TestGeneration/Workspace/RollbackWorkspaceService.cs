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
        return RollbackChangesAsync(cancellationToken);
    }

    public Task RollbackChangesAsync(CancellationToken cancellationToken = default)
    {
        using var repo = new Repository(_context.Project.DirectoryPath);
        var headCommit = repo.Head.Tip ??
                         throw new InvalidOperationException("Repository has no HEAD commit to reset to.");
        repo.Reset(ResetMode.Hard, headCommit);
        DeleteUntrackedFiles(repo);
        DeleteIgnoredBuildArtifacts(repo.Info.WorkingDirectory);
        _context.Project.Logger?.Debug("Repository rolled back to HEAD");
        return Task.CompletedTask;
    }

    public Task PersistAcceptedChangesAsync(string message, CancellationToken cancellationToken = default)
    {
        _context.Project.Logger?.Debug("Rollback workspace ignores persist request: {Message}", message);
        return Task.CompletedTask;
    }

    private static void DeleteUntrackedFiles(Repository repo)
    {
        var status = repo.RetrieveStatus(new StatusOptions());
        var workingDirectory = Path.GetFullPath(repo.Info.WorkingDirectory);
        var untrackedPaths = status.Untracked
            .Select(x => Path.GetFullPath(Path.Combine(workingDirectory, x.FilePath)))
            .Where(x => IsUnderWorkingDirectory(x, workingDirectory))
            .OrderByDescending(x => x.Length)
            .ToList();

        foreach (var path in untrackedPaths)
        {
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        var candidateDirectories = untrackedPaths
            .SelectMany(x => EnumerateParentDirectories(x, workingDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length);

        foreach (var directory in candidateDirectories)
        {
            if (string.Equals(directory, workingDirectory, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(directory) ||
                Directory.EnumerateFileSystemEntries(directory).Any())
                continue;

            Directory.Delete(directory, false);
        }
    }

    private static IEnumerable<string> EnumerateParentDirectories(string path, string workingDirectory)
    {
        var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;

        while (!string.IsNullOrWhiteSpace(directory) &&
               IsUnderWorkingDirectory(directory, workingDirectory))
        {
            yield return directory;
            directory = Path.GetDirectoryName(directory);
        }
    }

    private static bool IsUnderWorkingDirectory(string path, string workingDirectory)
    {
        var relativePath = Path.GetRelativePath(workingDirectory, path);
        return relativePath != "." &&
               !relativePath.StartsWith("..", StringComparison.Ordinal) &&
               !Path.IsPathRooted(relativePath);
    }

    private static void DeleteIgnoredBuildArtifacts(string workingDirectory)
    {
        var root = Path.GetFullPath(workingDirectory);
        var directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                       name.Equals("obj", StringComparison.OrdinalIgnoreCase);
            })
            .Where(path => IsUnderWorkingDirectory(Path.GetFullPath(path), root))
            .Where(path => !Path.GetRelativePath(root, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(path => path.Length)
            .ToList();

        foreach (var directory in directories)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
