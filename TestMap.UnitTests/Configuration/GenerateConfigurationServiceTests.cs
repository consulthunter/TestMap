using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Services.Configuration;
using TestMap.Services.TestGeneration.Providers.Abstractions;

namespace TestMap.UnitTests.Configuration;

public sealed class GenerateConfigurationServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    /// <summary>
    /// Verifies that GenerateConfiguration writes a default config file with the expected runtime paths and core defaults.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GenerateConfiguration_WritesExpectedConfigFile()
    {
        // Arrange
        var basePath = CreateTemporaryDirectory();
        var basePathParent = CreateTemporaryDirectory();
        var configDir = Path.Combine(basePath, "Config");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "default-config.json");
        var service = new GenerateConfigurationService(configPath, basePath, basePathParent);

        // Act
        service.GenerateConfiguration();

        // Assert
        Assert.True(File.Exists(configPath));

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<TestMapConfig>(json, ConfigJsonSerializer.CreateOptions());

        Assert.NotNull(config);
        Assert.Equal(Path.Combine(basePath, "Data", "example_project.txt"), config.RuntimeConfig.FilePaths.TargetFilePath);
        Assert.Equal(Path.Combine(basePath, "Logs"), config.RuntimeConfig.FilePaths.LogsDirPath);
        Assert.Equal(Path.Combine(basePathParent, "Temp"), config.RuntimeConfig.FilePaths.TempDirPath);
        Assert.Equal(Path.Combine(basePath, "Output"), config.RuntimeConfig.FilePaths.OutputDirPath);
        Assert.Equal("desktop-linux", config.RuntimeConfig.Docker.Context);
        Assert.Equal("net-sdk-all", config.RuntimeConfig.Docker.Image);
        Assert.True(config.RuntimeConfig.Project.KeepProjectFiles);
        Assert.Equal(5, config.RuntimeConfig.MaxConcurrency);
        Assert.Equal(AiProvider.OpenAi, config.TestingConfig.GenerationConfig.Provider);
        Assert.Equal(AiProviderMode.Chat, config.TestingConfig.GenerationConfig.Mode);
        Assert.Equal(3, config.TestingConfig.TestingFrameworks.Count);
        Assert.Equal("gpt-3.5-turbo", config.AiProviderConfig.OpenAi.Model);
        Assert.Equal("http://localhost:10000/", config.AiProviderConfig.Ollama.Endpoint);
        Assert.Equal("http://localhost:11434/", config.AiProviderConfig.CustomOpenAi.Endpoint);
        Assert.Equal("gemini-1.5-flash", config.AiProviderConfig.GoogleGemini.Model);
        Assert.Equal("us-central1", config.AiProviderConfig.GoogleCloud.Location);
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
        var path = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }
}
