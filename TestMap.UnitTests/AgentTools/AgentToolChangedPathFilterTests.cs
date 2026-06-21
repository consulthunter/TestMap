using TestMap.Services.AgentTools;

namespace TestMap.UnitTests.AgentTools;

public sealed class AgentToolChangedPathFilterTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(".testmap/evidence-summary.md")]
    [InlineData(".testmap/prompt.md")]
    [InlineData(".testmap/task-card.json")]
    [InlineData(".aider/session.json")]
    [InlineData(".claude/settings.local.json")]
    [InlineData(".codex/config.toml")]
    [InlineData(".gemini/history.json")]
    [InlineData(".openhands/state.json")]
    [InlineData(".mini-swe-agent/trajectory.json")]
    [InlineData("/workspace/.codex/session.json")]
    [InlineData(@".\.claude\state.json")]
    [InlineData(".CODEX/session.json")]
    public void IsExcluded_AgentMetadataPath_ReturnsTrue(string path)
    {
        Assert.True(AgentToolChangedPathFilter.IsExcluded(path));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("tests/FooTests.cs")]
    [InlineData("src/Program.cs")]
    [InlineData(".github/workflows/build.yml")]
    [InlineData("docs/.codex-example.md")]
    public void IsExcluded_RepositoryContent_ReturnsFalse(string path)
    {
        Assert.False(AgentToolChangedPathFilter.IsExcluded(path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Filter_RemovesMetadataAndDeduplicatesRemainingPaths()
    {
        var result = AgentToolChangedPathFilter.Filter(
        [
            ".testmap/prompt.md",
            ".codex/session.json",
            "tests/FooTests.cs",
            @"tests\FooTests.cs",
            "src/Program.cs"
        ]);

        Assert.Equal(2, result.Count);
        Assert.Contains("tests/FooTests.cs", result);
        Assert.Contains("src/Program.cs", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CountIncludedDeletedFiles_ExcludesAgentMetadata()
    {
        var count = AgentToolChangedPathFilter.CountIncludedDeletedFiles(
            """
            D  .testmap/prompt.md
             D .codex/session.json
            D  tests/RemovedTests.cs
             M src/Program.cs
            """);

        Assert.Equal(1, count);
    }
}
