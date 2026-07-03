using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Runtime;
using TestMap.Services.AgentTools;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="DockerToolRunner.ResolveImageName"/> covering image registry lookups,
/// ImageKey vs Id precedence, and diagnostic error messages on misses.
/// No Docker daemon is required.
/// </summary>
public sealed class DockerToolRunnerImageResolutionTests
{
    private static DockerToolRunner MakeRunner(Dictionary<string, string>? images = null)
    {
        var runtime = new RuntimeConfig
        {
            Docker = new DockerConfig
            {
                Images = new DockerImageRegistry
                {
                    AgentTools = images ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["codex"] = "testmap-agent-eval-codex:latest",
                        ["claude"] = "testmap-agent-eval-claude:latest",
                        ["aider"] = "testmap-agent-eval-aider:latest"
                    }
                }
            }
        };
        var resolver = new AgentToolEnvironmentResolver();
        return new DockerToolRunner(runtime, resolver);
    }

    /// <summary>
    /// ResolveImageName returns the registered image name for the "codex" key.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveImageName_KnownToolId_ReturnsRegisteredImage()
    {
        // Arrange
        var runner = MakeRunner();
        var tool = new ExperimentToolConfig { Id = "codex" };

        // Act
        var image = runner.ResolveImageName(tool);

        // Assert
        Assert.Equal("testmap-agent-eval-codex:latest", image);
    }

    /// <summary>
    /// ResolveImageName uses ImageKey when it is set, ignoring Id.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveImageName_ImageKeySet_UsesImageKeyNotId()
    {
        // Arrange
        var runner = MakeRunner();
        var tool = new ExperimentToolConfig { Id = "my-custom-codex-variant", ImageKey = "codex" };

        // Act
        var image = runner.ResolveImageName(tool);

        // Assert
        Assert.Equal("testmap-agent-eval-codex:latest", image);
    }

    /// <summary>
    /// ResolveImageName falls back to Id when ImageKey is null.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveImageName_NullImageKey_UsesId()
    {
        // Arrange
        var runner = MakeRunner();
        var tool = new ExperimentToolConfig { Id = "aider", ImageKey = null };

        // Act
        var image = runner.ResolveImageName(tool);

        // Assert
        Assert.Equal("testmap-agent-eval-aider:latest", image);
    }

    /// <summary>
    /// ResolveImageName throws InvalidOperationException for an unknown key and the message
    /// lists the available keys so callers can diagnose configuration problems quickly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveImageName_UnknownKey_ThrowsWithDiagnosticMessage()
    {
        // Arrange
        var runner = MakeRunner();
        var tool = new ExperimentToolConfig { Id = "nonexistent-tool" };

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => runner.ResolveImageName(tool));

        // Assert — message must name the missing key and list available ones
        Assert.Contains("nonexistent-tool", ex.Message);
        Assert.Contains("codex", ex.Message);
        Assert.Contains("claude", ex.Message);
    }

    /// <summary>
    /// Image key lookup is case-insensitive (AgentTools dict uses OrdinalIgnoreCase).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveImageName_DifferentCase_ReturnsImage()
    {
        // Arrange
        var runner = MakeRunner();
        var tool = new ExperimentToolConfig { Id = "CODEX" };

        // Act
        var image = runner.ResolveImageName(tool);

        // Assert
        Assert.Equal("testmap-agent-eval-codex:latest", image);
    }
}
