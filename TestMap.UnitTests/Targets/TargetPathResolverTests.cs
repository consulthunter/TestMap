using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class TargetPathResolverTests
{
    [Fact]
    public void Resolve_UsesRepositoryAndFullRevisionForEveryScope()
    {
        var root = Path.Combine(Path.GetTempPath(), "testmap-paths");
        var paths = new TargetPathResolver().Resolve(
            TargetTestData.Target(), root, root + "-out", root + "-logs",
            new DateTimeOffset(2026, 7, 16, 14, 5, 9, TimeSpan.Zero));
        Assert.Contains(Path.Combine("owner", "repository", TargetTestData.Commit), paths.WorkspacePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("analysis.db", paths.DatabasePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            Path.Combine("2026-07-16", "14-05-09_owner-repository", "run.log"),
            paths.LogPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(paths.WorkspacePath, paths.ArtifactPath);
    }

    [Theory]
    [InlineData("../escape/repo")]
    [InlineData("CON/repo")]
    [InlineData("CON.txt/repo")]
    [InlineData("owner/..")]
    public void Resolve_UnsafeIdentity_IsRejected(string repository)
    {
        var target = new RepositoryTarget("id", repository, "url", TargetTestData.Commit, [1]);
        Assert.Throws<InvalidDataException>(() => new TargetPathResolver().Resolve(target, "temp", "out", "logs"));
    }

    [Fact]
    public void Resolve_DifferentRevisions_CannotCollide()
    {
        var resolver = new TargetPathResolver();
        var first = resolver.Resolve(TargetTestData.Target(), "temp", "out", "logs");
        var second = resolver.Resolve(TargetTestData.Target(TargetTestData.OtherCommit), "temp", "out", "logs");
        Assert.NotEqual(first.WorkspacePath, second.WorkspacePath);
        Assert.NotEqual(first.DatabasePath, second.DatabasePath);
    }

    [Fact]
    public void Resolve_SharedWorkspaceAndOutputRoot_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            new TargetPathResolver().Resolve(TargetTestData.Target(), "same-root", "same-root", "logs"));
    }
}
