using TestMap.Models.Configuration;
using TestMap.Services.Configuration;

namespace TestMap.IntegrationTests.Configuration;

public sealed class ConfigurationServiceIntegrationTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    /// <summary>
    /// Verifies that ConfigureRunAsync reads a real target file, creates runtime directories, and initializes project models.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public async Task ConfigureRunAsync_WithRealTargetFile_InitializesProjectModelsFromDisk()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var logsPath = Path.Combine(rootPath, "Logs");
        var tempPath = Path.Combine(rootPath, "Temp");
        var outputPath = Path.Combine(rootPath, "Output");
        var dataPath = Path.Combine(rootPath, "Data");
        Directory.CreateDirectory(dataPath);

        var targetFilePath = Path.Combine(dataPath, "targets.txt");
        await File.WriteAllLinesAsync(
            targetFilePath,
            [
                "https://github.com/dotnet/runtime.git",
                "https://github.com/xunit/xunit"
            ]);

        var config = new TestMapConfig
        {
            RuntimeConfig =
            {
                FilePaths =
                {
                    LogsDirPath = logsPath,
                    TempDirPath = tempPath,
                    OutputDirPath = outputPath,
                    TargetFilePath = targetFilePath
                }
            }
        };
        var service = new ConfigurationService(config);

        // Act
        await service.ConfigureRunAsync();

        // Assert
        Assert.True(Directory.Exists(Path.Combine(logsPath, service.RunDate)));
        Assert.True(Directory.Exists(tempPath));
        Assert.True(Directory.Exists(outputPath));

        Assert.Equal(2, service.ProjectModels.Count);

        var runtimeProject = service.ProjectModels[0];
        Assert.Equal("dotnet", runtimeProject.Owner);
        Assert.Equal("runtime", runtimeProject.RepoName);
        Assert.Equal(Path.Combine(tempPath, "runtime"), runtimeProject.DirectoryPath);
        Assert.Equal(Path.Combine(outputPath, "dotnet-runtime", "analysis.db"), runtimeProject.DatabasePath);

        var xunitProject = service.ProjectModels[1];
        Assert.Equal("xunit", xunitProject.Owner);
        Assert.Equal("xunit", xunitProject.RepoName);
        Assert.Equal(Path.Combine(outputPath, "xunit-xunit", "analysis.db"), xunitProject.DatabasePath);
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

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }
}
