using TestMap.Services.AgentTools;

namespace TestMap.UnitTests.AgentTools;

public sealed class AgentToolLogPathResolverTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("codex")]
    [InlineData("claude")]
    [InlineData("copilot")]
    public void Resolve_JsonlStdOutTool_UsesJsonlForStdOutAndJsonl(string toolId)
    {
        var result = AgentToolLogPathResolver.Resolve("/attempt", toolId);

        Assert.Equal(Path.Combine("/attempt", $"{toolId}.events.jsonl"), result.StdOutLogPath);
        Assert.Equal(Path.Combine("/attempt", $"{toolId}.stderr.log"), result.StdErrLogPath);
        Assert.Equal(result.StdOutLogPath, result.JsonlLogPath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_OpenHands_ReturnsSeparateStdOutAndJsonlPaths()
    {
        var result = AgentToolLogPathResolver.Resolve("/attempt", "openhands");

        Assert.Equal(Path.Combine("/attempt", "openhands.stdout.log"), result.StdOutLogPath);
        Assert.Equal(Path.Combine("/attempt", "openhands.stderr.log"), result.StdErrLogPath);
        Assert.Equal(Path.Combine("/attempt", "openhands.events.jsonl"), result.JsonlLogPath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_GeminiStreamJson_UsesEventsJsonl()
    {
        var result = AgentToolLogPathResolver.Resolve(
            "/attempt",
            "gemini",
            new Dictionary<string, string> { ["gemini_output_format"] = "stream-json" });

        Assert.Equal(Path.Combine("/attempt", "gemini.events.jsonl"), result.StdOutLogPath);
        Assert.Equal(result.StdOutLogPath, result.JsonlLogPath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_RegularTextTool_LeavesJsonlPathEmpty()
    {
        var result = AgentToolLogPathResolver.Resolve("/attempt", "aider");

        Assert.Equal(Path.Combine("/attempt", "aider.stdout.log"), result.StdOutLogPath);
        Assert.Equal(Path.Combine("/attempt", "aider.stderr.log"), result.StdErrLogPath);
        Assert.Empty(result.JsonlLogPath);
    }
}
