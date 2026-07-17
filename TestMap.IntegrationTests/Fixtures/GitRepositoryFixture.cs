using LibGit2Sharp;

namespace TestMap.IntegrationTests.Fixtures;

public sealed class GitRepositoryFixture : IDisposable
{
    private readonly string _root;

    private GitRepositoryFixture(string root, string worktreePath, string remotePath, IReadOnlyList<string> commits)
    {
        _root = root;
        WorktreePath = worktreePath;
        RemotePath = remotePath;
        Commits = commits;
    }

    public string WorktreePath { get; }
    public string RemotePath { get; }
    public IReadOnlyList<string> Commits { get; }

    public string AddCommitAndPush(string contents = "later revision")
    {
        using var repository = new Repository(WorktreePath);
        File.WriteAllText(Path.Combine(WorktreePath, "revision.txt"), contents + "\n");
        Commands.Stage(repository, "revision.txt");
        var signature = new Signature("TestMap", "testmap@example.invalid", DateTimeOffset.UtcNow);
        var commit = repository.Commit("later revision", signature, signature);
        var origin = repository.Network.Remotes["origin"];
        repository.Network.Push(origin, $"{repository.Head.CanonicalName}:{repository.Head.CanonicalName}", new PushOptions());
        return commit.Sha;
    }

    public static GitRepositoryFixture Create(int commitCount = 2)
    {
        if (commitCount < 1)
            throw new ArgumentOutOfRangeException(nameof(commitCount));

        var root = Path.Combine(Path.GetTempPath(), "testmap-git-" + Guid.NewGuid().ToString("N"));
        var worktree = Path.Combine(root, "worktree");
        var remote = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(worktree);
        Repository.Init(worktree);

        var commits = new List<string>();
        using (var repository = new Repository(worktree))
        {
            var signature = new Signature("TestMap", "testmap@example.invalid", DateTimeOffset.UtcNow);
            for (var index = 1; index <= commitCount; index++)
            {
                File.WriteAllText(Path.Combine(worktree, "revision.txt"), $"revision-{index}\n");
                Commands.Stage(repository, "revision.txt");
                commits.Add(repository.Commit($"revision {index}", signature, signature).Sha);
            }
        }

        Repository.Init(remote, isBare: true);
        using (var repository = new Repository(worktree))
        {
            var origin = repository.Network.Remotes.Add("origin", remote);
            repository.Network.Push(origin, $"{repository.Head.CanonicalName}:{repository.Head.CanonicalName}", new PushOptions());
        }

        return new GitRepositoryFixture(root, worktree, remote, commits);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
    }
}
