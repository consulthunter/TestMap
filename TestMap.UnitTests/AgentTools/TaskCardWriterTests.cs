using System.Text.Json;
using TestMap.Services.Configuration;
using TestMap.Services.Experiment.TaskCards;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="TaskCardWriter"/> covering directory creation, file output, and
/// JSON round-trip of the task card. Uses a temp directory cleaned up in a finally block.
/// </summary>
public sealed class TaskCardWriterTests
{
    /// <summary>
    /// WriteAsync creates the .testmap subdirectory when it does not already exist.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_DirectoryDoesNotExist_CreatesDirectory()
    {
        // Arrange
        var workspace = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var writer = new TaskCardWriter();
            var content = MakeContent();

            // Act
            await writer.WriteAsync(workspace, content);

            // Assert
            Assert.True(Directory.Exists(Path.Combine(workspace, ".testmap")));
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>
    /// WriteAsync produces task-card.json, prompt.md, and evidence-summary.md in .testmap/.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_WritesAllThreeFiles()
    {
        // Arrange
        var workspace = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var writer = new TaskCardWriter();
            var content = MakeContent();

            // Act
            await writer.WriteAsync(workspace, content);

            // Assert
            var dir = Path.Combine(workspace, ".testmap");
            Assert.True(File.Exists(Path.Combine(dir, "task-card.json")));
            Assert.True(File.Exists(Path.Combine(dir, "prompt.md")));
            Assert.True(File.Exists(Path.Combine(dir, "evidence-summary.md")));
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>
    /// The written task-card.json deserializes back to a TaskCard with the original field values.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_TaskCardJson_RoundTripsAllFields()
    {
        // Arrange
        var workspace = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var writer = new TaskCardWriter();
            var card = new TaskCard
            {
                Objective = "TestSuiteExpansion",
                TargetMemberName = "Calculate",
                TargetMemberSignature = "public int Calculate(int x)",
                TargetMemberWeakness = "no branch coverage",
                AccessStrategy = "extend-existing",
                MappedTests = ["CalculatorTests.Calculate_ReturnsSum"],
                Constraints = ["Do not modify existing tests."]
            };
            var content = new TaskCardContent
            {
                Card = card,
                Prompt = "Write a test for Calculate.",
                EvidenceSummary = "Coverage: 50%"
            };

            // Act
            await writer.WriteAsync(workspace, content);

            // Assert
            var json = await File.ReadAllTextAsync(Path.Combine(workspace, ".testmap", "task-card.json"));
            var options = ConfigJsonSerializer.CreateOptions();
            var deserialized = JsonSerializer.Deserialize<TaskCard>(json, options);

            Assert.NotNull(deserialized);
            Assert.Equal(card.Objective, deserialized!.Objective);
            Assert.Equal(card.TargetMemberName, deserialized.TargetMemberName);
            Assert.Equal(card.TargetMemberSignature, deserialized.TargetMemberSignature);
            Assert.Equal(card.TargetMemberWeakness, deserialized.TargetMemberWeakness);
            Assert.Equal(card.AccessStrategy, deserialized.AccessStrategy);
            Assert.Equal(card.MappedTests, deserialized.MappedTests);
            Assert.Equal(card.Constraints, deserialized.Constraints);
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>
    /// The prompt content is written verbatim to prompt.md without modification.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_PromptContent_WrittenVerbatim()
    {
        // Arrange
        var workspace = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var writer = new TaskCardWriter();
            var prompt = "# Task\nWrite a test for `Calculate`.\n\nDetails follow...";
            var content = MakeContent(prompt: prompt);

            // Act
            await writer.WriteAsync(workspace, content);

            // Assert
            var written = await File.ReadAllTextAsync(Path.Combine(workspace, ".testmap", "prompt.md"));
            Assert.Equal(prompt, written);
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>
    /// WriteAsync is idempotent: calling it twice on the same workspace overwrites the files.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_CalledTwice_OverwritesPreviousFiles()
    {
        // Arrange
        var workspace = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var writer = new TaskCardWriter();

            // Act
            await writer.WriteAsync(workspace, MakeContent(prompt: "first"));
            await writer.WriteAsync(workspace, MakeContent(prompt: "second"));

            // Assert
            var written = await File.ReadAllTextAsync(Path.Combine(workspace, ".testmap", "prompt.md"));
            Assert.Equal("second", written);
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static TaskCardContent MakeContent(string? prompt = null) => new()
    {
        Card = new TaskCard
        {
            TargetMemberName = "TestMethod",
            TargetMemberSignature = "public void TestMethod()"
        },
        Prompt = prompt ?? "Write a test.",
        EvidenceSummary = "No mutations survived."
    };
}
