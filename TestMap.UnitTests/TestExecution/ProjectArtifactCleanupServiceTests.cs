using TestMap.App;
using TestMap.Models;
using TestMap.Services.TestExecution;
using TestMap.Rules.TestExecution;

namespace TestMap.UnitTests.TestExecution;

public sealed class ProjectArtifactCleanupServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    /// <summary>
    /// Verifies that cleanup removes coverage, mutation, and nested TestResults directories.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CleanupProjectDirectory_WhenArtifactsAreNotPreserved_RemovesGeneratedArtifacts()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        var mutationDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "mutation")).FullName;
        var nestedResultsDirectory =
            Directory.CreateDirectory(Path.Combine(projectDirectory, "src", "Sample.Tests", "TestResults")).FullName;
        var service = CreateService(projectDirectory);

        // Act
        service.CleanupProjectDirectory(false);

        // Assert
        Assert.False(Directory.Exists(coverageDirectory));
        Assert.False(Directory.Exists(mutationDirectory));
        Assert.False(Directory.Exists(nestedResultsDirectory));
    }

    /// <summary>
    /// Verifies that cleanup preserves generated artifacts when requested.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CleanupProjectDirectory_WhenArtifactsArePreserved_LeavesGeneratedArtifacts()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        var mutationDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "mutation")).FullName;
        var service = CreateService(projectDirectory);

        // Act
        service.CleanupProjectDirectory(true);

        // Assert
        Assert.True(Directory.Exists(coverageDirectory));
        Assert.True(Directory.Exists(mutationDirectory));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(true, "Preserve", "test-execution.cleanup.preserve-artifacts")]
    [InlineData(false, "Delete", "test-execution.cleanup.delete-artifacts")]
    public void DecideArtifactCleanup_WithPreserveFlag_ReturnsAuditableDecision(
        bool preserveArtifacts,
        string expectedValue,
        string expectedRuleId)
    {
        var decision = TestExecutionDecisionEngine.DecideArtifactCleanup(preserveArtifacts);

        Assert.Equal("ArtifactCleanup", decision.DecisionKind);
        Assert.Equal(expectedValue, decision.Value);
        Assert.Equal(expectedRuleId, decision.RuleId);
        Assert.Equal("1.0", decision.RuleVersion);
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

    private ProjectArtifactCleanupService CreateService(string projectDirectory)
    {
        return new ProjectArtifactCleanupService(new ProjectContext(new ProjectModel(directoryPath: projectDirectory)));
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }
}
