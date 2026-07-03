using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Services.Configuration;

namespace TestMap.IntegrationTests.Configuration;

public sealed class GenerateConfigurationServiceIntegrationTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    /// <summary>
    /// Verifies that GenerateConfiguration writes a real JSON file that can be deserialized into a usable TestMapConfig.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void GenerateConfiguration_WritesConsumableConfigFile()
    {
        // Arrange
        var parentPath = CreateTemporaryDirectory();
        var basePath = Path.Combine(parentPath, "Workspace");
        Directory.CreateDirectory(basePath);
        Directory.CreateDirectory(Path.Combine(basePath, "Config"));

        var configPath = Path.Combine(basePath, "Config", "default-config.json");
        var service = new GenerateConfigurationService(configPath, basePath, parentPath);

        // Act
        service.GenerateConfiguration();

        // Assert
        Assert.True(File.Exists(configPath));

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<TestMapConfig>(json, ConfigJsonSerializer.CreateOptions());

        Assert.NotNull(config);
        Assert.Equal(Path.Combine(basePath, "Data", "example_project.txt"), config.RuntimeConfig.FilePaths.TargetFilePath);
        Assert.Equal(Path.Combine(parentPath, "Temp"), config.RuntimeConfig.FilePaths.TempDirPath);
        Assert.Equal(3, config.TestingConfig.TestingFrameworks.Count);
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
