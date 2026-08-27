using TestMap.App;
using TestMap.Models;
using TestMap.Models.Coverage;
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
        await WriteSidecarAsync(coverageDirectory, "run-1");
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
        await WriteSidecarAsync(coverageDirectory, "run-1");
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
    /// Verifies that a missing collection sidecar is represented as an explicit failure.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithMissingSidecar_ReturnsExplicitCollectionFailure()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage"));
        var service = new CollectCoverageResultsService(CreateContext(projectDirectory));

        // Act
        var (report, raw, normalized) = await service.CollectAsync("run-1");

        // Assert
        Assert.NotNull(report);
        Assert.Equal("run-1", report.RunId);
        Assert.Equal("CollectionFailed", report.CollectionStatus);
        Assert.Contains("sidecar", report.CollectionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CoverageReportModel.CorrectedPolicyVersion, report.MeasurementPolicyVersion);
        Assert.False(report.HasUsableCoverage);
        Assert.Equal(string.Empty, raw);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithSidecarButNoArtifact_PreservesStatusMetadata()
    {
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        await WriteSidecarAsync(
            coverageDirectory,
            "run-2",
            status: "ProviderUnavailable",
            reason: "Preferred and fallback collectors were unavailable.");
        var service = new CollectCoverageResultsService(CreateContext(projectDirectory));

        var (report, _, _) = await service.CollectAsync("run-2");

        Assert.NotNull(report);
        Assert.Equal("ProviderUnavailable", report.CollectionStatus);
        Assert.Equal("XPlat Code Coverage", report.SuccessfulCollector);
        Assert.Contains("coverage-collection-v1", report.CollectionMetadataJson);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithMalformedArtifact_ReturnsParseFailure()
    {
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        await WriteSidecarAsync(coverageDirectory, "run-3");
        await File.WriteAllTextAsync(
            Path.Combine(coverageDirectory, "merged_run-3.cobertura.xml"),
            "<coverage><packages>");
        var service = new CollectCoverageResultsService(CreateContext(projectDirectory));

        var (report, _, _) = await service.CollectAsync("run-3");

        Assert.NotNull(report);
        Assert.Equal("ParseFailed", report.CollectionStatus);
        Assert.False(report.HasUsableCoverage);
        Assert.Equal("XPlat Code Coverage", report.SuccessfulCollector);
        Assert.Contains("coverage-collection-v1", report.CollectionMetadataJson);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithParsedEmptyArtifact_ReturnsParsedNoData()
    {
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        await WriteSidecarAsync(coverageDirectory, "run-4");
        await File.WriteAllTextAsync(
            Path.Combine(coverageDirectory, "merged_run-4.cobertura.xml"),
            "<coverage lines-covered=\"0\" lines-valid=\"0\" branches-covered=\"0\" branches-valid=\"0\"><packages /></coverage>");
        var service = new CollectCoverageResultsService(CreateContext(projectDirectory));

        var (report, _, _) = await service.CollectAsync("run-4");

        Assert.NotNull(report);
        Assert.Equal("ParsedNoData", report.CollectionStatus);
        Assert.True(report.LineCountsAvailable);
        Assert.True(report.BranchCountsAvailable);
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

    private static Task WriteSidecarAsync(
        string coverageDirectory,
        string runId,
        string status = "Merged",
        string reason = "")
    {
        return File.WriteAllTextAsync(
            Path.Combine(coverageDirectory, $"collection_{runId}.json"),
            $$"""
            {
              "schemaVersion": "coverage-collection-v1",
              "runId": "{{runId}}",
              "status": "{{status}}",
              "reason": "{{reason}}",
              "testReturnCode": 0,
              "successfulCollector": "XPlat Code Coverage",
              "providerAttempts": [],
              "merge": { "status": "{{status}}", "inputArtifacts": [], "duplicateArtifacts": [] }
            }
            """);
    }
}
