using LibGit2Sharp;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Targets;
using TestMap.Services.RepoOperations;
using TestMap.Services.Targets;
using TestMap.Services.TestGeneration.Workspace;

namespace TestMap.UnitTests.TestGeneration.Workspace;

public sealed class WorkspaceIntegrityServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "testmap-integrity-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EvaluateAsync_CleanPinnedWorkspace_IsVerifiedClean()
    {
        var context = CreateContext();
        var result = await new WorkspaceIntegrityService(context, new RepositoryUrlService()).EvaluateAsync(IntegrityCheckpoint.ExperimentStart);
        Assert.Equal(WorkspaceIntegrityStatus.VerifiedClean, result.Status);
        Assert.Equal(result.ExpectedCommit, result.ActualCommit);
        Assert.True(result.OriginMatches);
        Assert.False(result.WorkingTreeDirty);
    }

    [Fact]
    public async Task EvaluateAsync_DirtyWorkspace_DistinguishesExpectedFromUnexpectedChanges()
    {
        var context = CreateContext();
        await File.WriteAllTextAsync(Path.Combine(context.Project.DirectoryPath, "generated.cs"), "test");
        var service = new WorkspaceIntegrityService(context, new RepositoryUrlService());
        Assert.Equal(WorkspaceIntegrityStatus.UnexpectedChanges,
            (await service.EvaluateAsync(IntegrityCheckpoint.PreAttempt)).Status);
        Assert.Equal(WorkspaceIntegrityStatus.VerifiedExpectedChanges,
            (await service.EvaluateAsync(IntegrityCheckpoint.PostAttemptPreAnalysis, allowExpectedChanges: true)).Status);
    }

    [Fact]
    public async Task EvaluateAsync_MovedHead_IsRevisionMismatch()
    {
        var context = CreateContext();
        using (var repository = new Repository(context.Project.DirectoryPath))
        {
            var signature = new Signature("TestMap", "testmap@example.invalid", DateTimeOffset.UtcNow);
            await File.WriteAllTextAsync(Path.Combine(context.Project.DirectoryPath, "second.txt"), "second");
            Commands.Stage(repository, "second.txt");
            repository.Commit("second", signature, signature);
        }
        var result = await new WorkspaceIntegrityService(context, new RepositoryUrlService()).EvaluateAsync(IntegrityCheckpoint.PostAttemptPreAnalysis, true);
        Assert.Equal(WorkspaceIntegrityStatus.RevisionMismatch, result.Status);
    }

    [Fact]
    public async Task EvaluateAsync_WrongOrigin_IsOriginMismatch()
    {
        var context = CreateContext();
        using (var repository = new Repository(context.Project.DirectoryPath))
        {
            repository.Network.Remotes.Update("origin", remote => remote.Url = Path.Combine(_root, "other.git"));
        }
        var result = await new WorkspaceIntegrityService(context, new RepositoryUrlService()).EvaluateAsync(IntegrityCheckpoint.PreAttempt);
        Assert.Equal(WorkspaceIntegrityStatus.OriginMismatch, result.Status);
    }

    [Fact]
    public async Task EvaluateAsync_DeletedGitMetadata_IsRepositoryInvalid()
    {
        var context = CreateContext();
        var gitPath = Path.Combine(context.Project.DirectoryPath, ".git");
        foreach (var file in Directory.EnumerateFiles(gitPath, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(gitPath, true);
        var result = await new WorkspaceIntegrityService(context, new RepositoryUrlService()).EvaluateAsync(IntegrityCheckpoint.PreAttempt);
        Assert.Equal(WorkspaceIntegrityStatus.RepositoryInvalid, result.Status);
    }

    private ProjectContext CreateContext()
    {
        Directory.CreateDirectory(_root);
        var remote = Path.Combine(_root, "remote.git");
        var work = Path.Combine(_root, "work");
        Repository.Init(remote, true);
        Repository.Init(work);
        string commit;
        using (var repository = new Repository(work))
        {
            var signature = new Signature("TestMap", "testmap@example.invalid", DateTimeOffset.UtcNow);
            File.WriteAllText(Path.Combine(work, "tracked.txt"), "original");
            Commands.Stage(repository, "tracked.txt");
            commit = repository.Commit("initial", signature, signature).Sha;
            var origin = repository.Network.Remotes.Add("origin", remote);
            repository.Network.Push(origin, $"{repository.Head.CanonicalName}:{repository.Head.CanonicalName}", new PushOptions());
        }
        var target = new RepositoryTarget(TargetIdentityService.CreateTargetId("local/repository", commit), "local/repository", remote, commit, [1]);
        var project = new ProjectModel(remote, "local", "repository", directoryPath: work, databasePath: Path.Combine(_root, "analysis.db"));
        project.BindTarget(target);
        project.MaterializedRevision = new MaterializedRevision(target.TargetId, target.Repository, commit, commit, remote,
            new TargetPaths(work, project.DatabasePath!, Path.Combine(_root, "artifacts"), Path.Combine(_root, "run.log")),
            new string('a', 64), new string('b', 64), DateTimeOffset.UtcNow, MaterializationStatus.Available);
        return new ProjectContext(project);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }
}
