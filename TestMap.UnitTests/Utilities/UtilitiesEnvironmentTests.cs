using TestMap.Utilities;

namespace TestMap.UnitTests.Utilities;

public sealed class UtilitiesEnvironmentTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];
    private readonly string? _originalTestValue = Environment.GetEnvironmentVariable("TESTMAP_ENV_LOAD_TEST");

    [Fact]
    [Trait("Category", "Unit")]
    public void Load_WithConfigPath_LoadsEnvFromParentDirectory()
    {
        var root = CreateTemporaryDirectory();
        var configDirectory = Path.Combine(root, "TestMap", "Config");
        Directory.CreateDirectory(configDirectory);
        var configPath = Path.Combine(configDirectory, "openhands-tool-experiment.json");
        File.WriteAllText(configPath, "{}");
        File.WriteAllText(Path.Combine(root, ".env"), "TESTMAP_ENV_LOAD_TEST=from-dot-env");
        Environment.SetEnvironmentVariable("TESTMAP_ENV_LOAD_TEST", null);

        global::TestMap.Utilities.Utilities.Load(configPath);

        Assert.Equal("from-dot-env", Environment.GetEnvironmentVariable("TESTMAP_ENV_LOAD_TEST"));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("TESTMAP_ENV_LOAD_TEST", _originalTestValue);

        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
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
