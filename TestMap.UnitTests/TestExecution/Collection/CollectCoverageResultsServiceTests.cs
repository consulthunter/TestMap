using TestMap.App;
using TestMap.Models;
using TestMap.Services.TestExecution.Collection;

namespace TestMap.UnitTests.TestExecution.Collection;

public sealed class CollectCoverageResultsServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    /// <summary>
    /// Verifies that normalized Cobertura output is preferred from the report directory and raw coverage is returned when present.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithRawAndReportGeneratorCoverage_ReturnsParsedCoverageAndRawReports()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        var reportDirectory = Directory.CreateDirectory(Path.Combine(coverageDirectory, "report_run-1")).FullName;
        var rawPath = Path.Combine(coverageDirectory, "merged_run-1_raw.cobertura.xml");
        var normalizedPath = Path.Combine(reportDirectory, "Cobertura.xml");
        await File.WriteAllTextAsync(rawPath, "<coverage line-rate=\"0.12\" />");
        await File.WriteAllTextAsync(
            normalizedPath,
            """
            <coverage line-rate="0.75" branch-rate="0.5" complexity="1.5" version="1" timestamp="123" lines-covered="3" lines-valid="4" branches-covered="1" branches-valid="2">
              <packages />
            </coverage>
            """);
        var service = new CollectCoverageResultsService(CreateContext(projectDirectory));

        // Act
        var (report, raw, normalized) = await service.CollectAsync("run-1");

        // Assert
        Assert.NotNull(report);
        Assert.Equal(0.75, report.LineRate);
        Assert.Equal(0.5, report.BranchRate);
        Assert.Equal(1.5, report.ComplexityValue);
        Assert.Contains("line-rate=\"0.12\"", raw);
        Assert.Contains("line-rate=\"0.75\"", normalized);
    }

    /// <summary>
    /// Verifies that merged normalized Cobertura output is used when ReportGenerator output is absent.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithMergedNormalizedCoverage_ReturnsParsedCoverage()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        var mergedNormalizedPath = Path.Combine(coverageDirectory, "merged_run-1.cobertura.xml");
        await File.WriteAllTextAsync(
            mergedNormalizedPath,
            "<coverage line-rate=\"0.42\" branch-rate=\"0\" complexity=\"0\"><packages /></coverage>");
        var service = new CollectCoverageResultsService(CreateContext(projectDirectory));

        // Act
        var (report, raw, normalized) = await service.CollectAsync("run-1");

        // Assert
        Assert.NotNull(report);
        Assert.Equal(0.42, report.LineRate);
        Assert.Equal(string.Empty, raw);
        Assert.Contains("line-rate=\"0.42\"", normalized);
    }

    /// <summary>
    /// Verifies that missing coverage artifacts return an empty coverage model and empty report strings.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithMissingCoverageFiles_ReturnsEmptyCoverageModel()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage"));
        var service = new CollectCoverageResultsService(CreateContext(projectDirectory));

        // Act
        var (report, raw, normalized) = await service.CollectAsync("run-1");

        // Assert
        Assert.NotNull(report);
        Assert.Equal(0.0, report.LineRate);
        Assert.Equal(string.Empty, raw);
        Assert.Equal(string.Empty, normalized);
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
