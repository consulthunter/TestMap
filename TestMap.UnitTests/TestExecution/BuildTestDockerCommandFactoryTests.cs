using TestMap.Models.Code;
using TestMap.Rules.TestExecution;
using TestMap.Services.TestExecution;

namespace TestMap.UnitTests.TestExecution;

public sealed class BuildTestDockerCommandFactoryTests
{
    /// <summary>
    /// Verifies that Docker context selection chooses Windows only when required and otherwise avoids stale Windows contexts.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("", false, DockerRuntimePathMapper.LinuxContextName)]
    [InlineData(DockerRuntimePathMapper.WindowsContextName, false, DockerRuntimePathMapper.LinuxContextName)]
    [InlineData("custom-linux", false, "custom-linux")]
    [InlineData("custom-linux", true, DockerRuntimePathMapper.WindowsContextName)]
    public void ResolveDockerContext_WithConfiguredContextAndWindowsRequirement_ReturnsExpectedContext(
        string configuredContext,
        bool requiresWindows,
        string expected)
    {
        // Act
        var context = BuildTestDockerCommandFactory.ResolveDockerContext(
            configuredContext,
            requiresWindows,
            new DockerRuntimePathMapper());

        // Assert
        Assert.Equal(expected, context);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DecideDockerContext_WithWindowsRequirement_ReturnsVersionedRuleDecision()
    {
        var decision = TestExecutionDecisionEngine.DecideDockerContext(
            "custom-linux",
            requiresWindows: true,
            new DockerRuntimePathMapper());

        Assert.Equal("DockerContext", decision.DecisionKind);
        Assert.Equal(DockerRuntimePathMapper.WindowsContextName, decision.Value);
        Assert.Equal("test-execution.docker-context.windows-required", decision.RuleId);
        Assert.Equal("1.0", decision.RuleVersion);
    }

    /// <summary>
    /// Verifies that common baseline framework selection picks the newest shared modern target framework.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void TryResolveCommonBaselineTestFramework_WithSharedModernTargets_ReturnsNewestCommonFramework()
    {
        // Arrange
        var projects = new[]
        {
            CreateProject("A.Tests.csproj", ["net8.0", "net10.0"]),
            CreateProject("B.Tests.csproj", ["net6.0", "net8.0", "net10.0"])
        };

        // Act
        var framework = BuildTestDockerCommandFactory.TryResolveCommonBaselineTestFramework(projects);

        // Assert
        Assert.Equal("net10.0", framework);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DecideCommonBaselineTestFramework_WithSharedModernTargets_ReturnsAuditableDecision()
    {
        var projects = new[]
        {
            CreateProject("A.Tests.csproj", ["net8.0", "net10.0"]),
            CreateProject("B.Tests.csproj", ["net6.0", "net8.0", "net10.0"])
        };

        var decision = TestExecutionDecisionEngine.DecideCommonBaselineTestFramework(projects);

        Assert.Equal("BaselineTargetFramework", decision.DecisionKind);
        Assert.Equal("net10.0", decision.Value);
        Assert.Equal("test-execution.baseline-framework.shared-target", decision.RuleId);
        Assert.Equal("1.0", decision.RuleVersion);
        Assert.Equal(2, decision.Evidence.Count);
    }

    /// <summary>
    /// Verifies that baseline framework selection falls back to null when test projects share no target framework.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void TryResolveCommonBaselineTestFramework_WithNoSharedTarget_ReturnsNull()
    {
        // Arrange
        var projects = new[]
        {
            CreateProject("A.Tests.csproj", ["net8.0"]),
            CreateProject("B.Tests.csproj", ["net10.0"])
        };

        // Act
        var framework = BuildTestDockerCommandFactory.TryResolveCommonBaselineTestFramework(projects);

        // Assert
        Assert.Null(framework);
    }

    /// <summary>
    /// Verifies that target framework selection prefers BuildMetadata targets over discovery-time build targets.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ChoosePreferredTargetFramework_WithBuildMetadataTargets_PrefersMetadataTargets()
    {
        // Arrange
        var project = CreateProject(
            "A.Tests.csproj",
            buildTargets: ["net6.0"],
            metadataTargets: ["net8.0", "net10.0"]);

        // Act
        var framework = BuildTestDockerCommandFactory.ChoosePreferredTargetFramework(project);

        // Assert
        Assert.Equal("net10.0", framework);
    }

    /// <summary>
    /// Verifies that Windows requirement aggregation treats likely-required projects as requiring a Windows context.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(WindowsRequirementType.NotRequired, false)]
    [InlineData(WindowsRequirementType.LikelyRequired, true)]
    [InlineData(WindowsRequirementType.Required, true)]
    public void SolutionSetRequiresWindows_WithProjectRequirement_ReturnsExpectedDecision(
        WindowsRequirementType requirement,
        bool expected)
    {
        // Arrange
        var projects = new[]
        {
            CreateProject("A.Tests.csproj", ["net10.0"], windowsRequirement: requirement)
        };

        // Act
        var requiresWindows = BuildTestDockerCommandFactory.SolutionSetRequiresWindows(projects);

        // Assert
        Assert.Equal(expected, requiresWindows);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DecideSolutionWindowsRequirement_WithLikelyRequiredProject_ReturnsAuditableDecision()
    {
        var projects = new[]
        {
            CreateProject("A.Tests.csproj", ["net10.0"], windowsRequirement: WindowsRequirementType.LikelyRequired)
        };

        var decision = TestExecutionDecisionEngine.DecideSolutionWindowsRequirement(projects);

        Assert.Equal("RequiresWindows", decision.DecisionKind);
        Assert.Equal(bool.TrueString, decision.Value);
        Assert.Equal("test-execution.windows-requirement.project-required", decision.RuleId);
        Assert.Contains(decision.Evidence, x =>
            x.Key == "WindowsRequirement" &&
            x.Value == WindowsRequirementType.LikelyRequired.ToString());
    }

    /// <summary>
    /// Verifies that baseline test commands include the selected framework for Linux containers.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CreateBaselineTestsArgs_WithLinuxContextAndFramework_ReturnsExpectedDockerCommand()
    {
        // Act
        var args = BuildTestDockerCommandFactory.CreateBaselineTestsArgs(
            DockerRuntimePathMapper.LinuxContextName,
            "sample-testing",
            "-v \"D:\\repo:/app/project\"",
            "testmap-runner",
            "baseline_123",
            ["Sample.sln"],
            "net10.0");

        // Assert
        Assert.Equal(
            "--context desktop-linux run -d --name sample-testing -v \"D:\\repo:/app/project\" testmap-runner python3 -m testmap_runner dotnet-tests --run-id \"baseline_123\" --solutions \"Sample.sln\" --framework \"net10.0\"",
            args);
    }

    /// <summary>
    /// Verifies that baseline test commands omit framework arguments when no common framework exists.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CreateBaselineTestsArgs_WithNoFramework_OmitsFrameworkArgument()
    {
        // Act
        var args = BuildTestDockerCommandFactory.CreateBaselineTestsArgs(
            DockerRuntimePathMapper.LinuxContextName,
            "sample-testing",
            "-v \"D:\\repo:/app/project\"",
            "testmap-runner",
            "baseline_123",
            ["Sample.sln", "Other.sln"],
            null);

        // Assert
        Assert.DoesNotContain("--framework", args);
        Assert.Contains("--solutions \"Sample.sln,Other.sln\"", args);
    }

    /// <summary>
    /// Verifies that targeted Windows test commands use the Windows Python executable, container path, framework, and collector.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CreateTargetedTestsArgs_WithWindowsContext_ReturnsExpectedDockerCommand()
    {
        // Act
        var args = BuildTestDockerCommandFactory.CreateTargetedTestsArgs(
            DockerRuntimePathMapper.WindowsContextName,
            "sample-testing",
            "-v \"D:\\repo:C:\\app\\project\"",
            "testmap-runner",
            "iteration_123",
            @"C:\app\project\Tests\Tests.csproj",
            "net481",
            "Code Coverage;Format=Cobertura");

        // Assert
        Assert.Equal(
            "--context desktop-windows run -d --name sample-testing -v \"D:\\repo:C:\\app\\project\" testmap-runner C:\\Python312\\python.exe -m testmap_runner dotnet-test-project --run-id \"iteration_123\" --project \"C:\\app\\project\\Tests\\Tests.csproj\" --framework \"net481\" --collector \"Code Coverage;Format=Cobertura\"",
            args);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateTargetedMutationArgs_UsesTestProjectAndReportNameOnly()
    {
        var args = BuildTestDockerCommandFactory.CreateTargetedMutationArgs(
            DockerRuntimePathMapper.LinuxContextName,
            "sample-testing",
            "-v \"D:\\repo:/app/project\"",
            "testmap-runner",
            "iteration_123",
            "/app/project/src/Sample/Sample.csproj",
            "/app/project/tests/Sample.Tests/Sample.Tests.csproj");

        Assert.Equal(
            "--context desktop-linux run -d --name sample-testing -v \"D:\\repo:/app/project\" testmap-runner python3 -m testmap_runner dotnet-stryker-project --run-id \"iteration_123\" --report-name \"Sample\" --test-project \"/app/project/tests/Sample.Tests/Sample.Tests.csproj\"",
            args);
        Assert.DoesNotContain("--project ", args);
        Assert.DoesNotContain("--solution", args);
    }

    /// <summary>
    /// Verifies that coverage collector selection maps Coverlet to XPlat coverage and defaults unknown collectors to Microsoft coverage.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(CoverageCollectorType.Coverlet, "XPlat Code Coverage")]
    [InlineData(CoverageCollectorType.MicrosoftCodeCoverage, "Code Coverage;Format=Cobertura")]
    [InlineData(CoverageCollectorType.Unknown, "Code Coverage;Format=Cobertura")]
    public void ResolveCoverageCollectorArgument_WithCollectorType_ReturnsExpectedCollector(
        CoverageCollectorType collectorType,
        string expected)
    {
        // Act
        var collector = BuildTestDockerCommandFactory.ResolveCoverageCollectorArgument(collectorType);

        // Assert
        Assert.Equal(expected, collector);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TestExecutionRuleDefinitions_AllRulesHaveUniqueVersionedIds()
    {
        var rules = TestExecutionRuleDefinitions.All;

        Assert.All(rules, rule =>
        {
            Assert.StartsWith("test-execution.", rule.Id, StringComparison.Ordinal);
            Assert.Equal("1.0", rule.Version);
            Assert.Equal("TestExecution", rule.Category);
            Assert.False(string.IsNullOrWhiteSpace(rule.Description));
        });
        Assert.Equal(rules.Count, rules.Select(x => (x.Id, x.Version)).Distinct().Count());
    }

    private static CSharpProjectModel CreateProject(
        string filePath,
        List<string> buildTargets,
        List<string>? metadataTargets = null,
        WindowsRequirementType windowsRequirement = WindowsRequirementType.NotRequired)
    {
        var metadata = new ProjectBuildMetadataModel
        {
            IsTestProject = filePath.Contains(".Tests", StringComparison.OrdinalIgnoreCase),
            BuildTargets = metadataTargets ?? [],
            WindowsRequirement = windowsRequirement
        };
        return new CSharpProjectModel(
            [],
            [],
            buildTargets,
            metadata,
            filePath: filePath);
    }
}
