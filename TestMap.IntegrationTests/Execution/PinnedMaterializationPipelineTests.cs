using TestMap.App;
using TestMap.IntegrationTests.Fixtures;
using TestMap.Models;
using TestMap.Models.Targets;
using TestMap.Services.RepoOperations;
using TestMap.Services.Targets;

namespace TestMap.IntegrationTests.Execution;

public sealed class PinnedMaterializationPipelineTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MaterializationFailure_LeavesDatabaseAbsentAndPublishesTerminalTargetRow()
    {
        using var fixture = GitRepositoryFixture.Create();
        var root = Path.Combine(Path.GetTempPath(), "testmap-pipeline-" + Guid.NewGuid().ToString("N"));
        var commit = new string('f', 40);
        var target = new RepositoryTarget(TargetIdentityService.CreateTargetId("local/repository", commit), "local/repository", fixture.RemotePath, commit, [1]);
        var paths = new TargetPathResolver().Resolve(target, Path.Combine(root, "work"), Path.Combine(root, "out"), Path.Combine(root, "logs"));
        var project = new ProjectModel(fixture.RemotePath, "local", "repository", directoryPath: paths.WorkspacePath, databasePath: paths.DatabasePath);
        project.BindTarget(target);
        project.MaterializedRevision = new MaterializedRevision(target.TargetId, target.Repository, commit, null, null, paths,
            new string('a', 64), new string('b', 64), null, MaterializationStatus.Pending);
        var reportPath = Path.Combine(root, "target-execution.csv");
        var report = new TargetExecutionReportWriter(new AtomicFilePublisher());
        try
        {
            await report.InitializeAsync(reportPath, new TargetManifest(1, DateTimeOffset.UtcNow,
                new TargetSourceProvenance("input.csv", new string('b', 64)),
                new TargetReportReference("rejections.csv", new string('c', 64)), [target]), new string('a', 64));
            var service = new RepositoryMaterializationService(new ProjectContext(project), new RepositoryUrlService(), new RevisionWorkspaceLock());
            var failure = await Assert.ThrowsAsync<RepositoryMaterializationException>(() => service.MaterializeAsync());
            await report.UpdateAsync(target.TargetId, TargetExecutionStatus.CommitUnavailable, null, "Target materialization failed.", "materialization", failure.Status.ToString());
            Assert.False(File.Exists(paths.DatabasePath));
            var reportText = await File.ReadAllTextAsync(reportPath);
            Assert.Contains(",CommitUnavailable,", reportText, StringComparison.Ordinal);
            Assert.DoesNotContain(",Pending,", reportText, StringComparison.Ordinal);
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
}
