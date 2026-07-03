using TestMap.Services.TestExecution;

namespace TestMap.UnitTests.TestExecution;

public sealed class CallFailureServiceTests
{
    /// <summary>
    /// Verifies that Docker daemon failures are classified before later build/test patterns.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_WithDockerDaemonFailure_ReturnsInfrastructureClassification()
    {
        // Arrange
        var service = new CallFailureService();

        // Act
        var analysis = service.Analyze(
            "Cannot connect to the Docker daemon. Build FAILED. No test is available.");

        // Assert
        Assert.NotNull(analysis);
        Assert.Equal("infrastructure", analysis.Stage);
        Assert.Equal("docker_engine_failure", analysis.Category);
        Assert.Equal("docker", analysis.Source);
        Assert.Contains("Cannot connect to the Docker daemon", analysis.MatchedPatterns);
        Assert.Contains("Cannot connect to the Docker daemon", analysis.Evidence);
        Assert.NotNull(analysis.RuleDecision);
        Assert.Equal("FailureClassification", analysis.RuleDecision.DecisionKind);
        Assert.Equal("test-execution.failure.docker-engine", analysis.RuleDecision.RuleId);
        Assert.Equal("1.0", analysis.RuleDecision.RuleVersion);
    }

    /// <summary>
    /// Verifies that process diagnostics participate in known failure classification when logs are empty.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_WithRestoreDiagnostics_ReturnsPackageRestoreClassification()
    {
        // Arrange
        var service = new CallFailureService();

        // Act
        var analysis = service.Analyze(null, "error NU1101: Unable to find package Missing.Package");

        // Assert
        Assert.NotNull(analysis);
        Assert.Equal("restore", analysis.Stage);
        Assert.Equal("package_restore_failure", analysis.Category);
        Assert.Contains("NU1101", analysis.MatchedPatterns);
        Assert.Contains("NU1101", analysis.Evidence);
        Assert.NotNull(analysis.RuleDecision);
        Assert.Equal("test-execution.failure.package-restore", analysis.RuleDecision.RuleId);
    }

    /// <summary>
    /// Verifies that unmatched diagnostics return the generic unknown-failure classification with useful evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_WithUnknownFailure_ReturnsUnknownClassification()
    {
        // Arrange
        var service = new CallFailureService();

        // Act
        var analysis = service.Analyze("Unexpected exception while running tests.");

        // Assert
        Assert.NotNull(analysis);
        Assert.Equal("unknown", analysis.Stage);
        Assert.Equal("unknown_failure", analysis.Category);
        Assert.Empty(analysis.MatchedPatterns);
        Assert.Contains("Unexpected exception", analysis.Evidence);
        Assert.NotNull(analysis.RuleDecision);
        Assert.Equal("test-execution.failure.unknown", analysis.RuleDecision.RuleId);
    }

    /// <summary>
    /// Verifies that empty diagnostics do not produce a failure analysis.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_WithNoDiagnostics_ReturnsNull()
    {
        // Arrange
        var service = new CallFailureService();

        // Act
        var analysis = service.Analyze(null, " ");

        // Assert
        Assert.Null(analysis);
    }
}
