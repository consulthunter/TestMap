using System.IO;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestMap.Tests;

[TestSubject(typeof(Program))]

public class ProgramTest
{
    
    [Fact]
    public void LoadConfig_ShouldReturnSettings_WhenConfigFileExists()
    {
        // Arrange
        string configPath = Path.Combine(Path.GetTempPath(), "config.json");
        var json = new JObject { ["Setting1"] = "Value1" }.ToString();
        File.WriteAllText(configPath, json);

        // Act
        var settings = Program.LoadConfig(configPath);

        // Assert
        Assert.NotNull(settings);
        Assert.Equal("Value1", settings["Setting1"].ToString());

        // Cleanup
        File.Delete(configPath);
    }

    [Fact]
    public void LoadConfig_ShouldThrowFileNotFoundException_WhenConfigFileDoesNotExist()
    {
        // Arrange
        string configPath = Path.Combine(Path.GetTempPath(), "nonexistent_config.json");

        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() => Program.LoadConfig(configPath));
        Assert.Equal($"Config file not found: {configPath}", exception.Message);
    }

    [Fact]
    public void LoadConfig_ShouldThrowInvalidDataException_WhenConfigFileHasInvalidJson()
    {
        // Arrange
        string configPath = Path.Combine(Path.GetTempPath(), "invalid_config.json");
        File.WriteAllText(configPath, "invalid json");

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => Program.LoadConfig(configPath));
        Assert.Contains("Failed to parse config file as JSON.", exception.Message);

        // Cleanup
        File.Delete(configPath);
    }
}