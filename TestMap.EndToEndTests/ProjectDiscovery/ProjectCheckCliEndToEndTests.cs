namespace TestMap.EndToEndTests.ProjectDiscovery;

public sealed class ProjectCheckCliEndToEndTests
{
    [Fact]
    public async Task CheckProjects_LegacyInput_ReturnsFatalExitCodeBeforeNetworkAccess()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-check-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "targets.txt");
            await File.WriteAllTextAsync(input, "https://github.com/owner/repository.git\n");
            var exitCode = await TestMap.Program.Main(["check-projects", "--file", input]);
            Assert.Equal(1, exitCode);
            Assert.Empty(Directory.EnumerateFiles(directory, "*.yaml"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CheckProjects_UnknownPolicy_ReturnsFatalExitCodeBeforeReadingProvider()
    {
        var exitCode = await TestMap.Program.Main([
            "check-projects", "--file", "unused.yaml", "--policy", "unknown/v1"]);
        Assert.Equal(1, exitCode);
    }
}
