using TestMap.Models;
using TestMap.Models.Targets;
using TestMap.Services.Logging;

namespace TestMap.UnitTests.ProjectModels;

public sealed class ProjectModelLogDirectoryTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 16, 14, 5, 9, TimeSpan.Zero);

    [Fact]
    public void EnsureProjectLogDir_IsReadableAndIdempotent()
    {
        var root = CreateRoot();
        try
        {
            var project = new ProjectModel(
                owner: "PowerShell", repoName: "PlatyPS", logsDirPath: root,
                runStartedAtUtc: StartedAtUtc);
            var projectId = project.ProjectId;

            project.EnsureProjectLogDir();
            var first = project.LogsFilePath;
            project.EnsureProjectLogDir();

            Assert.Equal(first, project.LogsFilePath);
            Assert.Equal(projectId + ".log", Path.GetFileName(project.LogsFilePath));
            Assert.Equal("14-05-09_powershell-platyps", Path.GetFileName(Path.GetDirectoryName(project.LogsFilePath)));
            Assert.Single(Directory.EnumerateDirectories(Path.Combine(root, "2026-07-16")));
            (project.Logger as IDisposable)?.Dispose();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void EnsureProjectLogDir_PinnedRevisionUsesReadableDirectoryAndUpdatesEvidencePath()
    {
        var root = CreateRoot();
        try
        {
            var pinnedPath = Path.Combine(root, "owner", "repository", "commit", "run.log");
            var project = new ProjectModel(
                owner: "owner", repoName: "repository", logsDirPath: root,
                runStartedAtUtc: StartedAtUtc)
            {
                MaterializedRevision = new MaterializedRevision(
                    "target", "owner/repository", new string('a', 40), new string('a', 40), null,
                    new TargetPaths("workspace", "database", "artifacts", pinnedPath),
                    "manifest", "source", StartedAtUtc, MaterializationStatus.Available)
            };

            project.EnsureProjectLogDir();

            Assert.EndsWith(
                Path.Combine("2026-07-16", "14-05-09_owner-repository", "run.log"),
                project.LogsFilePath,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(project.LogsFilePath, project.MaterializedRevision.Paths.LogPath);
            Assert.NotEqual(pinnedPath, project.LogsFilePath);
            Assert.True(File.Exists(Path.Combine(
                Path.GetDirectoryName(project.LogsFilePath)!,
                ProjectLogDirectoryAllocator.ReservationMarkerFileName)));
            (project.Logger as IDisposable)?.Dispose();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void EnsureProjectLogDir_DoesNotModifyHistoricalRandomDirectory()
    {
        var root = CreateRoot();
        try
        {
            var historical = Path.Combine(root, "2026-07-16", "123456_owner-repository");
            Directory.CreateDirectory(historical);
            File.WriteAllText(Path.Combine(historical, "old.log"), "historical");
            var project = new ProjectModel(
                owner: "owner", repoName: "repository", logsDirPath: root,
                runStartedAtUtc: StartedAtUtc);

            project.EnsureProjectLogDir();

            Assert.Equal("historical", File.ReadAllText(Path.Combine(historical, "old.log")));
            Assert.NotEqual(historical, Path.GetDirectoryName(project.LogsFilePath));
            (project.Logger as IDisposable)?.Dispose();
        }
        finally { Directory.Delete(root, true); }
    }

    private static string CreateRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "testmap-project-logs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
