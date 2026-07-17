using LibGit2Sharp;
using TestMap.Services.RepoOperations;

namespace TestMap.UnitTests.RepoOperations;

public sealed class RepositoryManagedArtifactExclusionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    [Trait("Category", "Unit")]
    public void Ensure_UntrackedRuntimeArtifacts_AreIgnoredIdempotently()
    {
        Repository.Init(_root);
        Directory.CreateDirectory(Path.Combine(_root, "coverage"));
        Directory.CreateDirectory(Path.Combine(_root, "mutation"));
        Directory.CreateDirectory(Path.Combine(_root, "src", "Sample.Tests", "TestResults"));
        File.WriteAllText(Path.Combine(_root, "coverage", "coverage.xml"), "coverage");
        File.WriteAllText(Path.Combine(_root, "mutation", "mutation.json"), "mutation");
        File.WriteAllText(Path.Combine(_root, "src", "Sample.Tests", "TestResults", "results.trx"), "results");

        using var repository = new Repository(_root);
        Assert.True(repository.RetrieveStatus().IsDirty);

        RepositoryManagedArtifactExclusions.Ensure(repository);
        RepositoryManagedArtifactExclusions.Ensure(repository);

        Assert.False(repository.RetrieveStatus().IsDirty);
        var exclude = File.ReadAllText(Path.Combine(repository.Info.Path, "info", "exclude"));
        Assert.Equal(1, CountOccurrences(exclude, "/coverage/"));
        Assert.Equal(1, CountOccurrences(exclude, "/mutation/"));
        Assert.Equal(1, CountOccurrences(exclude, "**/TestResults/"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Ensure_TrackedArtifactNamedPath_DoesNotHideSourceChanges()
    {
        Repository.Init(_root);
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(_root, "coverage")).FullName;
        var trackedPath = Path.Combine(coverageDirectory, "tracked-source.txt");
        File.WriteAllText(trackedPath, "baseline");
        using var repository = new Repository(_root);
        Commands.Stage(repository, "coverage/tracked-source.txt");
        var signature = new Signature("TestMap", "testmap@example.test", DateTimeOffset.UtcNow);
        repository.Commit("baseline", signature, signature);

        RepositoryManagedArtifactExclusions.Ensure(repository);
        File.WriteAllText(trackedPath, "changed");

        Assert.True(repository.RetrieveStatus().IsDirty);
        Assert.Contains(repository.RetrieveStatus(), entry =>
            entry.FilePath == "coverage/tracked-source.txt" && entry.State.HasFlag(FileStatus.ModifiedInWorkdir));
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    private static int CountOccurrences(string value, string expected)
    {
        return value.Split(expected, StringSplitOptions.None).Length - 1;
    }
}
