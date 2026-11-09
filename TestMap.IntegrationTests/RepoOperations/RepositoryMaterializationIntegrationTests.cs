using LibGit2Sharp;
using TestMap.App;
using TestMap.IntegrationTests.Fixtures;
using TestMap.Models;
using TestMap.Models.Targets;
using TestMap.Services.RepoOperations;
using TestMap.Services.Targets;

namespace TestMap.IntegrationTests.RepoOperations;

public sealed class RepositoryMaterializationIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MaterializeAsync_FreshAndExistingClone_ChecksOutExactDetachedCommit()
    {
        using var fixture = GitRepositoryFixture.Create();
        var root = TempRoot();
        try
        {
            var context = Context(fixture.RemotePath, fixture.Commits[0], root);
            var service = new RepositoryMaterializationService(context, new RepositoryUrlService(), new RevisionWorkspaceLock());
            var first = await service.MaterializeAsync();
            Assert.Equal(fixture.Commits[0], first.ResolvedCommit);
            using (var repository = new Repository(first.Paths.WorkspacePath))
                Assert.True(repository.Info.IsHeadDetached);

            var second = await service.MaterializeAsync();
            Assert.Equal(first.ResolvedCommit, second.ResolvedCommit);
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MaterializeAsync_DirtyExistingWorkspace_FailsWithoutDiscardingChanges()
    {
        using var fixture = GitRepositoryFixture.Create();
        var root = TempRoot();
        try
        {
            var context = Context(fixture.RemotePath, fixture.Commits[0], root);
            var service = new RepositoryMaterializationService(context, new RepositoryUrlService(), new RevisionWorkspaceLock());
            var revision = await service.MaterializeAsync();
            await File.WriteAllTextAsync(Path.Combine(revision.Paths.WorkspacePath, "researcher.txt"), "do not discard");
            var exception = await Assert.ThrowsAsync<RepositoryMaterializationException>(() => service.MaterializeAsync());
            Assert.Equal(MaterializationStatus.WorkspaceDirty, exception.Status);
            Assert.True(File.Exists(Path.Combine(revision.Paths.WorkspacePath, "researcher.txt")));
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MaterializeAsync_UnavailableCommit_FailsExplicitly()
    {
        using var fixture = GitRepositoryFixture.Create();
        var root = TempRoot();
        try
        {
            var context = Context(fixture.RemotePath, new string('f', 40), root);
            var service = new RepositoryMaterializationService(context, new RepositoryUrlService(), new RevisionWorkspaceLock());
            var exception = await Assert.ThrowsAsync<RepositoryMaterializationException>(() => service.MaterializeAsync());
            Assert.Equal(MaterializationStatus.CommitUnavailable, exception.Status);
            Assert.Null(context.VerifiedBaseCommit);
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MaterializeAsync_WrongOrigin_FailsWithoutReusingRepository()
    {
        using var expected = GitRepositoryFixture.Create();
        using var unrelated = GitRepositoryFixture.Create();
        var root = TempRoot();
        try
        {
            var context = Context(expected.RemotePath, expected.Commits[0], root);
            Directory.CreateDirectory(Path.GetDirectoryName(context.Project.DirectoryPath)!);
            Repository.Clone(unrelated.RemotePath, context.Project.DirectoryPath);
            var service = new RepositoryMaterializationService(context, new RepositoryUrlService(), new RevisionWorkspaceLock());
            var exception = await Assert.ThrowsAsync<RepositoryMaterializationException>(() => service.MaterializeAsync());
            Assert.Equal(MaterializationStatus.WrongOrigin, exception.Status);
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MaterializeAsync_CorruptExistingDirectory_FailsExplicitly()
    {
        using var fixture = GitRepositoryFixture.Create();
        var root = TempRoot();
        try
        {
            var context = Context(fixture.RemotePath, fixture.Commits[0], root);
            Directory.CreateDirectory(context.Project.DirectoryPath);
            await File.WriteAllTextAsync(Path.Combine(context.Project.DirectoryPath, "not-git.txt"), "unrelated");
            var service = new RepositoryMaterializationService(context, new RepositoryUrlService(), new RevisionWorkspaceLock());
            var exception = await Assert.ThrowsAsync<RepositoryMaterializationException>(() => service.MaterializeAsync());
            Assert.Equal(MaterializationStatus.InvalidWorkspace, exception.Status);
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MaterializeAsync_ExistingCloneMissingObject_FetchesRequestedCommit()
    {
        using var fixture = GitRepositoryFixture.Create(1);
        var root = TempRoot();
        var oldClone = Path.Combine(root, "old-clone");
        try
        {
            Directory.CreateDirectory(root);
            Repository.Clone(fixture.RemotePath, oldClone);
            var laterCommit = fixture.AddCommitAndPush();
            var context = Context(fixture.RemotePath, laterCommit, root);
            Directory.CreateDirectory(Path.GetDirectoryName(context.Project.DirectoryPath)!);
            Directory.Move(oldClone, context.Project.DirectoryPath);
            var revision = await new RepositoryMaterializationService(context, new RepositoryUrlService(), new RevisionWorkspaceLock())
                .MaterializeAsync();
            Assert.Equal(laterCommit, revision.ResolvedCommit);
        }
        finally { DeleteTree(root); }
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

    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "testmap-materialize-" + Guid.NewGuid().ToString("N"));

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }
}
