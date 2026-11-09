using TestMap.App;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Runtime;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Services.TestExecution.Collection;

namespace TestMap.UnitTests.TestExecution;

public sealed class CollectTestsResultWriterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveCsvPath_PinnedProject_UsesTargetListOutputRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        var outputRoot = Path.Combine(root, "Output");
        var project = new ProjectModel(directoryPath: Path.Combine(root, "workspace"))
        {
            OutputPath = Path.Combine(outputRoot, "owner", "repository", new string('a', 40), "artifacts")
        };
        var config = new TestMapConfig
        {
            RuntimeConfig = new RuntimeConfig
            {
                FilePaths = new FilePathConfig { OutputDirPath = outputRoot }
            }
        };

        var path = CollectTestsResultWriter.ResolveCsvPath(config, new ProjectContext(project));

        Assert.Equal(Path.GetFullPath(Path.Combine(outputRoot, "project-validation.csv")), path);
        Assert.DoesNotContain(Path.Combine("owner", "repository"), path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InitializeReportAsync_ExistingAggregate_ReplacesRowsFromPriorTargetListRun()
    {
        var root = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        try
        {
            var outputRoot = Path.Combine(root, "Output");
            Directory.CreateDirectory(outputRoot);
            var csvPath = Path.Combine(outputRoot, "project-validation.csv");
            await File.WriteAllTextAsync(csvPath, "old-header\nold-row\n");
            var config = new TestMapConfig
            {
                RuntimeConfig = new RuntimeConfig
                {
                    FilePaths = new FilePathConfig { OutputDirPath = outputRoot }
                }
            };

            await CollectTestsResultWriter.InitializeReportAsync(
                config,
                new ProjectContext(new ProjectModel(directoryPath: root)));

            Assert.Equal(
                CollectTestsResultWriter.CsvHeader + Environment.NewLine,
                await File.ReadAllTextAsync(csvPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("CollectionFailed", false, 0, 0, false)]
    [InlineData("ParsedNoData", false, 0, 0, false)]
    [InlineData("ParsedNoUsableCoverage", false, 2, 0, false)]
    [InlineData("PartiallyMapped", true, 2, 1, true)]
    [InlineData("Mapped", true, 1, 1, true)]
    [Trait("Category", "Unit")]
    public void CreateCoverageSummary_CorrectedStatus_UsesUsableFlagNotReportExistence(
        string status,
        bool hasUsableCoverage,
        int rawObjects,
        int mappedObjects,
        bool expectedHasCoverage)
    {
        var summary = CollectTestsResultWriter.CreateCoverageSummary(new CoverageReportEntity
        {
            CollectionStatus = status,
            CollectionReason = "fixture",
            MeasurementPolicyVersion = CoverageReportModel.CorrectedPolicyVersion,
            HasUsableCoverage = hasUsableCoverage,
            RawObjectCount = rawObjects,
            MappedObjectCount = mappedObjects,
            RawMemberCount = rawObjects * 2,
            MappedMemberCount = mappedObjects * 2
        });

        Assert.Equal(expectedHasCoverage, summary.HasCoverage);
        Assert.Equal(status, summary.Status);
        Assert.Equal(rawObjects, summary.RawObjectCount);
        Assert.Equal(mappedObjects * 2, summary.MappedMemberCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateCoverageSummary_LegacyReport_LeavesReconciliationCountsBlank()
    {
        var summary = CollectTestsResultWriter.CreateCoverageSummary(new CoverageReportEntity
        {
            CollectionStatus = CoverageReportModel.LegacyCollectionStatus,
            HasUsableCoverage = true,
            RawObjectCount = 99,
            MappedObjectCount = 99
        });

        Assert.False(summary.HasCoverage);
        Assert.Null(summary.RawObjectCount);
        Assert.Null(summary.MappedObjectCount);
        Assert.Equal(string.Empty, summary.PolicyVersion);
    }
}
