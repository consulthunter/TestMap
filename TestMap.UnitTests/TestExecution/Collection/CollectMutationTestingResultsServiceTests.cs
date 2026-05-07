using TestMap.App;
using TestMap.Models;
using TestMap.Services.TestExecution.Collection;

namespace TestMap.UnitTests.TestExecution.Collection;

public sealed class CollectMutationTestingResultsServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    /// <summary>
    /// Verifies that Stryker mutation reports are loaded from the expected solution/run report directory.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithMutationReport_ReturnsDeserializedReportAndRawJson()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        var reportDirectory = Directory.CreateDirectory(
            Path.Combine(projectDirectory, "mutation", "Sample_run-1", "reports")).FullName;
        await File.WriteAllTextAsync(
            Path.Combine(reportDirectory, "mutation-report.json"),
            """
            {
              "projectRoot": "/app/project",
              "schemaVersion": "1.0",
              "files": {},
              "testFiles": {}
            }
            """);
        var service = new CollectMutationTestingResultsService(CreateContext(projectDirectory));

        // Act
        var (reports, raw) = await service.CollectAsync("run-1", ["Sample.sln"]);

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal("/app/project", report.projectRoot);
        Assert.Equal("1.0", report.schemaVersion);
        Assert.Contains("\"projectRoot\": \"/app/project\"", raw);
    }

    /// <summary>
    /// Verifies that missing mutation reports are skipped without failing collection.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithMissingMutationReport_ReturnsEmptyResults()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        var service = new CollectMutationTestingResultsService(CreateContext(projectDirectory));

        // Act
        var (reports, raw) = await service.CollectAsync("run-1", ["Missing.sln"]);

        // Assert
        Assert.Empty(reports);
        Assert.Equal(string.Empty, raw);
    }

    public void Dispose()
    {
        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private ProjectContext CreateContext(string projectDirectory)
    {
        return new ProjectContext(new ProjectModel(directoryPath: projectDirectory));
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }
}
