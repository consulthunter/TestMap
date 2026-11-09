using Serilog;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Services.Configuration;
using TestMap.Services.Logging;
using TestMap.Services.Targets.Contracts;

namespace TestMap.IntegrationTests.Configuration;

public sealed class ReadableLogDirectoryIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task PinnedManifest_UsesReadableLogAndKeepsRevisionScopedNonLogPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "testmap-readable-pinned-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sourcePath = Path.Combine(root, "targets.csv");
            var manifestPath = Path.Combine(root, "targets.yaml");
            await File.WriteAllTextAsync(
                sourcePath,
                "name,lastCommitSHA\nowner/repository,0123456789abcdef0123456789abcdef01234567\n");
            await TestMap.Program.CreateTargetManifestAsync(
                sourcePath, manifestPath, TargetDelimiter.Comma);
            var config = new TestMapConfig
            {
                RuntimeConfig =
                {
                    FilePaths =
                    {
                        LogsDirPath = Path.Combine(root, "Logs"),
                        TempDirPath = Path.Combine(root, "Temp"),
                        OutputDirPath = Path.Combine(root, "Output"),
                        TargetFilePath = manifestPath
                    }
                }
            };
            var timestamp = new DateTimeOffset(2026, 7, 16, 14, 5, 9, TimeSpan.Zero);
            var service = new ConfigurationService(config, runStartedAtUtc: timestamp);
            await service.ConfigureRunAsync();
            var project = service.ProjectModels[0];
            var target = project.RepositoryTarget!;

            project.EnsureProjectLogDir();

            Assert.EndsWith(
                Path.Combine("2026-07-16", $"14-05-09_{target.Repository.Replace('/', '-')}", "run.log"),
                project.LogsFilePath,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(project.LogsFilePath, project.MaterializedRevision?.Paths.LogPath);
            Assert.Contains(Path.Combine(target.Repository.Replace('/', Path.DirectorySeparatorChar), target.Commit), project.DirectoryPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(target.Commit, project.DatabasePath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(target.Commit, project.OutputPath, StringComparison.OrdinalIgnoreCase);
            (project.Logger as IDisposable)?.Dispose();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SameRepositoryAndSecond_CreateDistinctLogsWithoutMergedContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "testmap-readable-log-collision-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var timestamp = new DateTimeOffset(2026, 7, 16, 14, 5, 9, TimeSpan.Zero);
            var first = new ProjectModel(owner: "owner", repoName: "repository", logsDirPath: root, runStartedAtUtc: timestamp);
            var second = new ProjectModel(owner: "owner", repoName: "repository", logsDirPath: root, runStartedAtUtc: timestamp);

            first.EnsureProjectLogDir();
            second.EnsureProjectLogDir();
            first.Logger?.Information("first-run-only");
            second.Logger?.Information("second-run-only");
            (first.Logger as IDisposable)?.Dispose();
            (second.Logger as IDisposable)?.Dispose();

            Assert.NotEqual(first.LogsFilePath, second.LogsFilePath);
            Assert.Equal("14-05-09_owner-repository", Path.GetFileName(Path.GetDirectoryName(first.LogsFilePath)));
            Assert.Equal("14-05-09_owner-repository-02", Path.GetFileName(Path.GetDirectoryName(second.LogsFilePath)));
            Assert.Contains("first-run-only", File.ReadAllText(first.LogsFilePath!));
            Assert.DoesNotContain("second-run-only", File.ReadAllText(first.LogsFilePath!));
            Assert.Contains("second-run-only", File.ReadAllText(second.LogsFilePath!));
            Assert.DoesNotContain("first-run-only", File.ReadAllText(second.LogsFilePath!));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConfigurationAndProjectLogging_CreateDistinctReadableLogsForOneRunInstant()
    {
        var root = Path.Combine(Path.GetTempPath(), "testmap-readable-log-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var targetPath = Path.Combine(root, "targets.txt");
        await File.WriteAllLinesAsync(targetPath,
        [
            "https://github.com/owner/first",
            "https://github.com/owner/second"
        ]);
        try
        {
            var config = new TestMapConfig
            {
                RuntimeConfig =
                {
                    FilePaths =
                    {
                        LogsDirPath = Path.Combine(root, "Logs"),
                        TempDirPath = Path.Combine(root, "Temp"),
                        OutputDirPath = Path.Combine(root, "Output"),
                        TargetFilePath = targetPath
                    }
                }
            };
            var timestamp = new DateTimeOffset(2026, 7, 16, 14, 5, 9, TimeSpan.Zero);
            var service = new ConfigurationService(config, runStartedAtUtc: timestamp);
            await service.ConfigureRunAsync();

            foreach (var project in service.ProjectModels)
            {
                project.EnsureProjectLogDir();
                project.Logger?.Information("integration {Repository}", project.RepoName);
                (project.Logger as IDisposable)?.Dispose();
            }

            Assert.Collection(service.ProjectModels.OrderBy(project => project.RepoName),
                first => Assert.Contains(Path.Combine("2026-07-16", "14-05-09_owner-first"), first.LogsFilePath, StringComparison.OrdinalIgnoreCase),
                second => Assert.Contains(Path.Combine("2026-07-16", "14-05-09_owner-second"), second.LogsFilePath, StringComparison.OrdinalIgnoreCase));
            Assert.All(service.ProjectModels, project =>
            {
                Assert.True(File.Exists(project.LogsFilePath));
                Assert.True(File.Exists(Path.Combine(
                    Path.GetDirectoryName(project.LogsFilePath)!,
                    ProjectLogDirectoryAllocator.ReservationMarkerFileName)));
            });
        }
        finally { Directory.Delete(root, true); }
    }

}
