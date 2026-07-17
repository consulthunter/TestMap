using TestMap.App;
using TestMap.IntegrationTests.Fixtures;
using TestMap.Models;
using TestMap.Models.Targets;
using TestMap.Services.RepoOperations;
using TestMap.Services.Targets;

namespace TestMap.IntegrationTests.Targets;

public sealed class MultiRevisionIsolationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SameRepositoryTwoCommits_ConcurrentMaterializationUsesIndependentScopes()
    {
        using var fixture = GitRepositoryFixture.Create();
        var root = Path.Combine(Path.GetTempPath(), "testmap-multirevision-" + Guid.NewGuid().ToString("N"));
        try
        {
            var contexts = fixture.Commits.Select(commit => Context(fixture.RemotePath, commit, root)).ToArray();
            var revisions = await Task.WhenAll(contexts.Select(context =>
                new RepositoryMaterializationService(context, new RepositoryUrlService(), new RevisionWorkspaceLock()).MaterializeAsync()));
            Assert.Equal(2, revisions.Select(item => item.Paths.WorkspacePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(2, revisions.Select(item => item.Paths.DatabasePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(fixture.Commits.Order(), revisions.Select(item => item.ResolvedCommit!).Order());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
        }
    }

    private static ProjectContext Context(string url, string commit, string root)
    {
        var target = new RepositoryTarget(TargetIdentityService.CreateTargetId("local/repository", commit), "local/repository", url, commit, [1]);
        var paths = new TargetPathResolver().Resolve(target, Path.Combine(root, "work"), Path.Combine(root, "out"), Path.Combine(root, "logs"));
        var project = new ProjectModel(url, "local", "repository", directoryPath: paths.WorkspacePath, databasePath: paths.DatabasePath);
        project.BindTarget(target);
        project.MaterializedRevision = new MaterializedRevision(target.TargetId, target.Repository, commit, null, null, paths,
            new string('a', 64), new string('b', 64), null, MaterializationStatus.Pending);
        return new ProjectContext(project);
    }
}
