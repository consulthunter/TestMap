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

    [Fact]
    [Trait("Category", "Unit")]
    public void CopilotRunner_AliasesTestMapTokenToCopilotCliToken()
    {
        var dockerRoot = ResolveAgentToolsRoot();
        var runner = File.ReadAllText(Path.Combine(dockerRoot, "copilot", "run-copilot.sh"));

        Assert.Contains("COPILOT_GITHUB_TOKEN", runner);
        Assert.Contains("GITHUB_COPILOT_TOKEN", runner);
        Assert.Contains("export COPILOT_GITHUB_TOKEN=\"${GITHUB_COPILOT_TOKEN}\"", runner);
        Assert.Contains("COPILOT_GITHUB_TOKEN_SET=", runner);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CopilotRunner_EnablesJsonlOutputAndOtelUsageExport()
    {
        var dockerRoot = ResolveAgentToolsRoot();
        var runner = File.ReadAllText(Path.Combine(dockerRoot, "copilot", "run-copilot.sh"));

        Assert.Contains("--output-format json", runner);
        Assert.Contains("--no-color", runner);
        Assert.Contains("COPILOT_OTEL_FILE_EXPORTER_PATH", runner);
        Assert.Contains("/attempt/copilot-otel.jsonl", runner);
        Assert.Contains("/attempt/copilot.events.jsonl", runner);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MiniSweAgentRunner_GeneratesCustomEndpointModelConfig()
    {
        var dockerRoot = ResolveAgentToolsRoot();
        var runner = File.ReadAllText(Path.Combine(dockerRoot, "mini-swe-agent", "run-mini.sh"));

        Assert.Contains("MINI_API_BASE", runner);
        Assert.Contains("MINI_PROVIDER", runner);
        Assert.Contains("--config mini.yaml", runner);
        Assert.Contains("MINI_INCLUDE_DEFAULT_CONFIG", runner);
        Assert.Contains("/attempt/mini-custom-model.yaml", runner);
        Assert.Contains("api_base:", runner);
        Assert.Contains("custom_llm_provider:", runner);
        Assert.Contains("cost_tracking: ignore_errors", runner);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OpenHandsRunner_UsesWorkspaceTempDirectoryForAtomicFileEdits()
    {
        var dockerRoot = ResolveAgentToolsRoot();
        var runner = File.ReadAllText(Path.Combine(dockerRoot, "openhands", "run-openhands.sh"));

        Assert.Contains("export TMPDIR=\"${OPENHANDS_TMPDIR:-/workspace/.testmap/tmp}\"", runner);
        Assert.Contains("mkdir -p \"$TMPDIR\"", runner);
        Assert.Contains("TMPDIR=${TMPDIR}", runner);
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
