using TestMap.App;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Runtime;
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
}
