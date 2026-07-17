using LibGit2Sharp;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Targets;
using TestMap.Services.RepoOperations;
using TestMap.Services.Targets;
using TestMap.Services.TestGeneration.Workspace;

namespace TestMap.EndToEndTests;

public sealed class PinnedRepositoryTargetEndToEndTests
{
    [Fact]
    [Trait("Category", "EndToEnd")]
    public async Task TwoRevisions_LlmChangesAndRevisionMovingAgent_AreIsolatedAndRestored()
    {
        var root = Path.Combine(Path.GetTempPath(), "testmap-pinned-e2e-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var remote = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(source);
        Repository.Init(source);
        var commits = new List<string>();
        using (var repository = new Repository(source))
        {
            var signature = new Signature("TestMap", "testmap@example.invalid", DateTimeOffset.UtcNow);
            for (var index = 1; index <= 2; index++)
            {
                File.WriteAllText(Path.Combine(source, "source.cs"), $"revision {index}");
                Commands.Stage(repository, "source.cs");
                commits.Add(repository.Commit($"revision {index}", signature, signature).Sha);
            }
        }
        Repository.Init(remote, true);
        using (var repository = new Repository(source))
        {
            var origin = repository.Network.Remotes.Add("origin", remote);
            repository.Network.Push(origin, $"{repository.Head.CanonicalName}:{repository.Head.CanonicalName}", new PushOptions());
        }

        try
        {
            var llm = Context(remote, commits[0], root);
            var agent = Context(remote, commits[1], root);
            await Task.WhenAll(
                new RepositoryMaterializationService(llm, new RepositoryUrlService(), new RevisionWorkspaceLock()).MaterializeAsync(),
                new RepositoryMaterializationService(agent, new RepositoryUrlService(), new RevisionWorkspaceLock()).MaterializeAsync());

            await File.WriteAllTextAsync(Path.Combine(llm.Project.DirectoryPath, "GeneratedTests.cs"), "test");
            var llmObservation = await new WorkspaceIntegrityService(llm, new RepositoryUrlService())
                .EvaluateAsync(IntegrityCheckpoint.PostAttemptPreAnalysis, allowExpectedChanges: true);
            Assert.Equal(WorkspaceIntegrityStatus.VerifiedExpectedChanges, llmObservation.Status);
            await new RollbackWorkspaceService(llm).RollbackChangesAsync();

            using (var repository = new Repository(agent.Project.DirectoryPath))
            {
                File.WriteAllText(Path.Combine(agent.Project.DirectoryPath, "agent.txt"), "agent");
                Commands.Stage(repository, "agent.txt");
                var signature = new Signature("Agent", "agent@example.invalid", DateTimeOffset.UtcNow);
                repository.Commit("agent moved revision", signature, signature);
            }
            var agentObservation = await new WorkspaceIntegrityService(agent, new RepositoryUrlService())
                .EvaluateAsync(IntegrityCheckpoint.PostAttemptPreAnalysis, allowExpectedChanges: true);
            Assert.Equal(WorkspaceIntegrityStatus.RevisionMismatch, agentObservation.Status);
            await new RollbackWorkspaceService(agent).RollbackChangesAsync();

            Assert.Equal(commits[0], llm.VerifiedBaseCommit);
            Assert.Equal(commits[1], agent.VerifiedBaseCommit);
            Assert.NotEqual(llm.Project.DirectoryPath, agent.Project.DirectoryPath);
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
