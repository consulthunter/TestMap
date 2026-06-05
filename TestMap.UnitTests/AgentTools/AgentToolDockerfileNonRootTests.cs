namespace TestMap.UnitTests.AgentTools;

public sealed class AgentToolDockerfileNonRootTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void BaseDockerfile_DefinesSharedNonRootAgentUser()
    {
        var dockerRoot = ResolveAgentToolsRoot();
        var dockerfile = File.ReadAllText(Path.Combine(dockerRoot, "testmap-dotnet-agent-base", "Dockerfile"));

        Assert.Contains("useradd", dockerfile);
        Assert.Contains("testmap-agent", dockerfile);
        Assert.Contains("ENV HOME=/home/testmap-agent", dockerfile);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("aider")]
    [InlineData("claude")]
    [InlineData("codex")]
    [InlineData("copilot")]
    [InlineData("gemini")]
    [InlineData("mini-swe-agent")]
    [InlineData("openhands")]
    public void ToolDockerfile_RunsEntrypointAsNonRootAgentUser(string toolName)
    {
        var dockerRoot = ResolveAgentToolsRoot();
        var dockerfile = File.ReadAllText(Path.Combine(dockerRoot, toolName, "Dockerfile"));

        Assert.Contains("USER testmap-agent", dockerfile);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RunnerLibrary_CapturesUntrackedFilesInChangedFileListAndPatch()
    {
        var dockerRoot = ResolveAgentToolsRoot();
        var runner = File.ReadAllText(Path.Combine(dockerRoot, "common", "agent-runner-lib.sh"));

        Assert.Contains("ls-files --others --exclude-standard", runner);
        Assert.Contains("changed-files.txt", runner);
        Assert.Contains("diff --binary --no-index -- /dev/null", runner);
    }

    private static string ResolveAgentToolsRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "TestMap", "Docker", "agentic-tools");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate TestMap/Docker/agentic-tools.");
    }
}
