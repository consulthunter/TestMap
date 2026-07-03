using Microsoft.CodeAnalysis;
using TestMap.Models.Code;
using TestMap.Models.Rules;
using TestMap.Services.ProjectDiscovery;
using TestMap.Rules.ProjectDiscovery;

namespace TestMap.UnitTests.ProjectDiscovery;

public sealed class ProjectBuildAnalysisServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    [Fact]
    [Trait("Category", "Unit")]
    public void ProjectDiscoveryRuleDefinitions_AllRulesHaveUniqueVersionedIds()
    {
        var rules = ProjectDiscoveryRuleDefinitions.All;

        Assert.All(rules, rule =>
        {
            Assert.StartsWith("project-discovery.", rule.Id, StringComparison.Ordinal);
            Assert.Equal("1.0", rule.Version);
            Assert.Equal("ProjectDiscovery", rule.Category);
            Assert.False(string.IsNullOrWhiteSpace(rule.Description));
        });
        Assert.Equal(rules.Count, rules.Select(x => (x.Id, x.Version)).Distinct().Count());
    }

    /// <summary>
    /// Verifies that project analysis captures target frameworks, SDK version, test-project status, coverage collector, and non-Windows execution.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithCrossPlatformXunitProject_ReturnsBuildAndExecutionMetadata()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(rootPath, "global.json"),
            """
            {
              "sdk": {
                "version": "10.0.100"
              }
            }
            """);
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "Sample.Tests",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="coverlet.collector" Version="6.0.4" />
              </ItemGroup>
            </Project>
            """);
        var service = CreateService();

        // Act
        var metadata = await service.AnalyzeAsync(CreateProject("Sample.Tests", projectPath));

        // Assert
        Assert.Equal(["net8.0", "net10.0"], metadata.BuildTargets);
        Assert.Equal("net8.0", metadata.DefaultBuildTarget);
        Assert.Equal(Path.Combine(rootPath, "global.json"), metadata.GlobalJsonPath);
        Assert.Equal("10.0.100", metadata.SdkVersion);
        Assert.True(metadata.IsTestProject);
        Assert.False(metadata.UsesWindowsDesktop);
        Assert.Equal(WindowsRequirementType.NotRequired, metadata.WindowsRequirement);
        Assert.Equal(ExecutionSupportType.Supported, metadata.ExecutionSupport);
        Assert.Equal(CoverageCollectorType.Coverlet, metadata.CoverageCollector);
        AssertDecision(
            metadata,
            "BuildTargets",
            "net8.0;net10.0",
            "project-discovery.build-targets.target-frameworks",
            RuleConfidence.Medium,
            ("ProjectXml", "TargetFrameworks", "net8.0;net10.0"));
        AssertDecision(
            metadata,
            "SdkVersion",
            "10.0.100",
            "project-discovery.sdk-version.global-json",
            RuleConfidence.Medium,
            ("GlobalJson", "sdk.version", "10.0.100"));
        AssertDecision(
            metadata,
            "IsTestProject",
            bool.TrueString,
            "project-discovery.test-project.name",
            RuleConfidence.Medium,
            ("RoslynProject", "Name", "Sample.Tests"));
        AssertDecision(
            metadata,
            "CoverageCollector",
            CoverageCollectorType.Coverlet.ToString(),
            "project-discovery.coverage-collector.coverlet",
            RuleConfidence.High,
            ("ProjectXml", "PackageReference", "coverlet.collector"));
        Assert.Contains("targets: net8.0, net10.0", metadata.Notes);
        Assert.Contains("coverage: Coverlet", metadata.Notes);
        Assert.Contains("windows: NotRequired", metadata.Notes);
        Assert.Contains("support: Supported", metadata.Notes);
    }

    /// <summary>
    /// Verifies that Windows-targeted desktop projects are marked as requiring a Windows execution environment.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithWindowsDesktopProject_ReturnsWindowsRequired()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "Desktop.Tests",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0-windows10.0.19041</TargetFramework>
                <UseWPF>true</UseWPF>
              </PropertyGroup>
            </Project>
            """);
        var service = CreateService();

        // Act
        var metadata = await service.AnalyzeAsync(CreateProject("Desktop.Tests", projectPath));

        // Assert
        Assert.Equal(["net8.0-windows10.0.19041"], metadata.BuildTargets);
        Assert.Equal("net8.0-windows10.0.19041", metadata.DefaultBuildTarget);
        Assert.True(metadata.IsTestProject);
        Assert.True(metadata.UsesWindowsDesktop);
        Assert.Equal(WindowsRequirementType.Required, metadata.WindowsRequirement);
        Assert.Equal(ExecutionSupportType.Supported, metadata.ExecutionSupport);
        Assert.Equal(CoverageCollectorType.Unknown, metadata.CoverageCollector);
        AssertDecision(
            metadata,
            "WindowsRequirement",
            WindowsRequirementType.Required.ToString(),
            "project-discovery.windows-requirement.desktop",
            RuleConfidence.High,
            ("ProjectXml", "WindowsDesktop", "UseWPF/UseWindowsForms"));
    }

    /// <summary>
    /// Verifies that non-Windows platform target frameworks are marked unsupported by the generic runner.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("net8.0-android", ExecutionSupportType.UnsupportedWorkload)]
    [InlineData("net8.0-ios", ExecutionSupportType.UnsupportedWorkload)]
    [InlineData("net8.0-maccatalyst", ExecutionSupportType.UnsupportedWorkload)]
    [InlineData("net8.0-tvos", ExecutionSupportType.UnsupportedWorkload)]
    [InlineData("net8.0-browser", ExecutionSupportType.UnsupportedWorkload)]
    [InlineData("net8.0-customos", ExecutionSupportType.UnsupportedPlatform)]
    public async Task AnalyzeAsync_WithUnsupportedPlatformTargetFramework_ReturnsUnsupportedExecutionSupport(
        string targetFramework,
        ExecutionSupportType expected)
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "MobileLibrary",
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{{targetFramework}}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var service = CreateService();

        // Act
        var metadata = await service.AnalyzeAsync(CreateProject("MobileLibrary", projectPath));

        // Assert
        Assert.Equal([targetFramework], metadata.BuildTargets);
        Assert.Equal(WindowsRequirementType.NotRequired, metadata.WindowsRequirement);
        Assert.Equal(expected, metadata.ExecutionSupport);
        AssertDecision(
            metadata,
            "ExecutionSupport",
            expected.ToString(),
            "project-discovery.execution-support.unsupported-target-framework",
            RuleConfidence.High,
            ("ProjectXml", "TargetFramework", targetFramework));
        Assert.Contains($"support: {expected}", metadata.Notes);
    }

    /// <summary>
    /// Verifies that explicit non-Windows TargetPlatformIdentifier values are marked unsupported by the generic runner.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithUnsupportedTargetPlatformIdentifier_ReturnsUnsupportedExecutionSupport()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "AndroidLibrary",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <TargetPlatformIdentifier>Android</TargetPlatformIdentifier>
              </PropertyGroup>
            </Project>
            """);
        var service = CreateService();

        // Act
        var metadata = await service.AnalyzeAsync(CreateProject("AndroidLibrary", projectPath));

        // Assert
        Assert.Equal(WindowsRequirementType.NotRequired, metadata.WindowsRequirement);
        Assert.Equal(ExecutionSupportType.UnsupportedWorkload, metadata.ExecutionSupport);
        AssertDecision(
            metadata,
            "ExecutionSupport",
            ExecutionSupportType.UnsupportedWorkload.ToString(),
            "project-discovery.execution-support.unsupported-target-platform",
            RuleConfidence.High,
            ("ProjectXml", "TargetPlatformIdentifier", "Android"));
    }

    /// <summary>
    /// Verifies that Windows runtime identifiers without desktop properties are marked as likely requiring Windows.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithWindowsRuntimeIdentifier_ReturnsWindowsLikelyRequired()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "RuntimeSpecific",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <RuntimeIdentifier>win-x64</RuntimeIdentifier>
              </PropertyGroup>
            </Project>
            """);
        var service = CreateService();

        // Act
        var metadata = await service.AnalyzeAsync(CreateProject("RuntimeSpecific", projectPath));

        // Assert
        Assert.Equal("win-x64", metadata.RuntimeIdentifier);
        Assert.Equal(WindowsRequirementType.LikelyRequired, metadata.WindowsRequirement);
        Assert.False(metadata.UsesWindowsDesktop);
        Assert.False(metadata.IsTestProject);
        AssertDecision(
            metadata,
            "WindowsRequirement",
            WindowsRequirementType.LikelyRequired.ToString(),
            "project-discovery.windows-requirement.runtime-identifier",
            RuleConfidence.Medium,
            ("ProjectXml", "RuntimeIdentifier", "win-x64"));
    }

    /// <summary>
    /// Verifies that SDK-style legacy .NET Framework targets require a Windows build environment.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithLegacyNetFrameworkTarget_ReturnsWindowsRequired()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "LegacyLibrary",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net472;netstandard2.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);
        var service = CreateService();

        // Act
        var metadata = await service.AnalyzeAsync(CreateProject("LegacyLibrary", projectPath));

        // Assert
        Assert.Equal(["net472", "netstandard2.0"], metadata.BuildTargets);
        Assert.Equal(WindowsRequirementType.Required, metadata.WindowsRequirement);
        AssertDecision(
            metadata,
            "WindowsRequirement",
            WindowsRequirementType.Required.ToString(),
            "project-discovery.windows-requirement.legacy-net-framework",
            RuleConfidence.High,
            ("ProjectXml", "TargetFramework", "net472"));
    }

    /// <summary>
    /// Verifies that old non-SDK-style TargetFrameworkVersion values are normalized and require Windows.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithLegacyTargetFrameworkVersion_NormalizesTargetAndReturnsWindowsRequired()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "LegacyTests",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
              </PropertyGroup>
            </Project>
            """);
        var service = CreateService();

        // Act
        var metadata = await service.AnalyzeAsync(CreateProject("LegacyTests", projectPath));

        // Assert
        Assert.Equal(["net48"], metadata.BuildTargets);
        Assert.Equal("net48", metadata.DefaultBuildTarget);
        Assert.Equal(WindowsRequirementType.Required, metadata.WindowsRequirement);
        AssertDecision(
            metadata,
            "BuildTargets",
            "net48",
            "project-discovery.build-targets.target-framework-version",
            RuleConfidence.Medium,
            ("ProjectXml", "TargetFrameworkVersion", "v4.8"));
        AssertDecision(
            metadata,
            "WindowsRequirement",
            WindowsRequirementType.Required.ToString(),
            "project-discovery.windows-requirement.legacy-net-framework",
            RuleConfidence.High,
            ("ProjectXml", "TargetFramework", "net48"));
    }

    /// <summary>
    /// Verifies that explicit IsTestProject and Microsoft code coverage packages are captured even without conventional test project names.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithExplicitTestProjectAndMicrosoftCoverage_ReturnsTestMetadata()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "QualityChecks",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeCoverage" Version="17.14.1" />
              </ItemGroup>
            </Project>
            """);
        var service = CreateService();

        // Act
        var metadata = await service.AnalyzeAsync(CreateProject("QualityChecks", projectPath));

        // Assert
        Assert.True(metadata.IsTestProject);
        Assert.Equal(CoverageCollectorType.MicrosoftCodeCoverage, metadata.CoverageCollector);
        Assert.Equal(WindowsRequirementType.NotRequired, metadata.WindowsRequirement);
        AssertDecision(
            metadata,
            "IsTestProject",
            bool.TrueString,
            "project-discovery.test-project.explicit",
            RuleConfidence.High,
            ("ProjectXml", "IsTestProject", "true"));
        AssertDecision(
            metadata,
            "CoverageCollector",
            CoverageCollectorType.MicrosoftCodeCoverage.ToString(),
            "project-discovery.coverage-collector.microsoft",
            RuleConfidence.High,
            ("ProjectXml", "PackageReference", "Microsoft.CodeCoverage"));
    }

    /// <summary>
    /// Verifies that missing project files produce a safe metadata result instead of throwing.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithMissingProjectFile_ReturnsMissingPathNote()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var missingProjectPath = Path.Combine(rootPath, "Missing.csproj");
        var service = CreateService();

        // Act
        var metadata = await service.AnalyzeAsync(CreateProject("Missing", missingProjectPath));

        // Assert
        Assert.Empty(metadata.BuildTargets);
        Assert.False(metadata.IsTestProject);
        Assert.Equal(WindowsRequirementType.Unknown, metadata.WindowsRequirement);
        Assert.Equal("Project file path is missing.", metadata.Notes);
        AssertDecision(
            metadata,
            "ProjectFile",
            "Missing",
            "project-discovery.project-file.missing",
            RuleConfidence.High);
    }

    /// <summary>
    /// Verifies that a singular TargetFramework takes precedence over TargetFrameworks when both are present.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithTargetFrameworkAndTargetFrameworks_UsesSingularTargetFramework()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "ConflictingTargets",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var metadata = await CreateService().AnalyzeAsync(CreateProject("ConflictingTargets", projectPath));

        // Assert
        Assert.Equal(["net10.0"], metadata.BuildTargets);
        Assert.Equal("net10.0", metadata.DefaultBuildTarget);
        AssertDecision(
            metadata,
            "BuildTargets",
            "net10.0",
            "project-discovery.build-targets.target-framework",
            RuleConfidence.Medium,
            ("ProjectXml", "TargetFramework", "net10.0"));
    }

    /// <summary>
    /// Verifies that multi-value target and runtime properties are trimmed and de-duplicated case-insensitively.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithDuplicateMultiValueProperties_ReturnsDistinctTrimmedValues()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "MultiTarget",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks> net8.0 ; NET8.0 ; net10.0 </TargetFrameworks>
                <RuntimeIdentifiers> linux-x64 ; win-x64 ; WIN-X64 </RuntimeIdentifiers>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var metadata = await CreateService().AnalyzeAsync(CreateProject("MultiTarget", projectPath));

        // Assert
        Assert.Equal(["net8.0", "net10.0"], metadata.BuildTargets);
        Assert.Equal(["linux-x64", "win-x64"], metadata.RuntimeIdentifiers);
        Assert.Equal(WindowsRequirementType.LikelyRequired, metadata.WindowsRequirement);
        AssertDecision(
            metadata,
            "WindowsRequirement",
            WindowsRequirementType.LikelyRequired.ToString(),
            "project-discovery.windows-requirement.runtime-identifiers",
            RuleConfidence.Medium,
            ("ProjectXml", "RuntimeIdentifiers", "win-x64"));
    }

    /// <summary>
    /// Verifies that PackageReference Update attributes are included when detecting test and coverage packages.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithPackageReferenceUpdateAttributes_DetectsPackages()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "PackageUpdateProject",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Update="NUnit" Version="4.0.0" />
                <PackageReference Update="Microsoft.Testing.Extensions.CodeCoverage" Version="17.14.1" />
              </ItemGroup>
            </Project>
            """);

        // Act
        var metadata = await CreateService().AnalyzeAsync(CreateProject("PackageUpdateProject", projectPath));

        // Assert
        Assert.True(metadata.IsTestProject);
        Assert.Equal(CoverageCollectorType.MicrosoftCodeCoverage, metadata.CoverageCollector);
        AssertDecision(
            metadata,
            "IsTestProject",
            bool.TrueString,
            "project-discovery.test-project.package",
            RuleConfidence.High,
            ("ProjectXml", "PackageReference", "NUnit"));
    }

    /// <summary>
    /// Verifies that invalid global.json files are tolerated while still reporting the file path.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithInvalidGlobalJson_ReturnsEmptySdkVersionAndGlobalJsonPath()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var globalJsonPath = Path.Combine(rootPath, "global.json");
        await File.WriteAllTextAsync(globalJsonPath, "{ invalid json");
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "InvalidGlobalJson",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var metadata = await CreateService().AnalyzeAsync(CreateProject("InvalidGlobalJson", projectPath));

        // Assert
        Assert.Equal(globalJsonPath, metadata.GlobalJsonPath);
        Assert.Equal(string.Empty, metadata.SdkVersion);
        AssertDecision(
            metadata,
            "SdkVersion",
            string.Empty,
            "project-discovery.sdk-version.unreadable-global-json",
            RuleConfidence.Low,
            ("GlobalJson", "Path", globalJsonPath));
    }

    /// <summary>
    /// Verifies that Windows target platform and Windows package references influence execution environment detection.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("<TargetPlatformIdentifier>Windows</TargetPlatformIdentifier>", WindowsRequirementType.Required)]
    [InlineData("<UseWindowsForms>true</UseWindowsForms>", WindowsRequirementType.Required)]
    [InlineData("<PackageReference Include=\"Microsoft.Windows.Compatibility\" Version=\"8.0.0\" />", WindowsRequirementType.LikelyRequired)]
    public async Task AnalyzeAsync_WithWindowsSignals_ReturnsExpectedWindowsRequirement(
        string projectSignal,
        WindowsRequirementType expected)
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var isPackageReference = projectSignal.Contains("PackageReference", StringComparison.Ordinal);
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "WindowsSignal",
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                {{(isPackageReference ? string.Empty : projectSignal)}}
              </PropertyGroup>
              <ItemGroup>
                {{(isPackageReference ? projectSignal : string.Empty)}}
              </ItemGroup>
            </Project>
            """);

        // Act
        var metadata = await CreateService().AnalyzeAsync(CreateProject("WindowsSignal", projectPath));

        // Assert
        Assert.Equal(expected, metadata.WindowsRequirement);
        AssertDecision(
            metadata,
            "WindowsRequirement",
            expected.ToString(),
            expected == WindowsRequirementType.Required
                ? projectSignal.Contains("TargetPlatformIdentifier", StringComparison.Ordinal)
                    ? "project-discovery.windows-requirement.target-platform"
                    : "project-discovery.windows-requirement.desktop"
                : "project-discovery.windows-requirement.package",
            expected == WindowsRequirementType.Required ? RuleConfidence.High : RuleConfidence.Medium);
    }

    /// <summary>
    /// Verifies that malformed project XML produces safe default metadata instead of throwing.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_WithMalformedProjectXml_ReturnsSafeDefaults()
    {
        // Arrange
        var rootPath = CreateTemporaryDirectory();
        var projectPath = await WriteProjectFileAsync(
            rootPath,
            "Malformed",
            "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework>");

        // Act
        var metadata = await CreateService().AnalyzeAsync(CreateProject("Malformed", projectPath));

        // Assert
        Assert.Empty(metadata.BuildTargets);
        Assert.False(metadata.IsTestProject);
        Assert.Equal(WindowsRequirementType.Unknown, metadata.WindowsRequirement);
        Assert.Equal(CoverageCollectorType.Unknown, metadata.CoverageCollector);
        AssertDecision(
            metadata,
            "ProjectFile",
            "ParseFailed",
            "project-discovery.project-file.parse-failed",
            RuleConfidence.High);
        AssertDecision(
            metadata,
            "WindowsRequirement",
            WindowsRequirementType.Unknown.ToString(),
            "project-discovery.windows-requirement.no-build-targets",
            RuleConfidence.Unknown);
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

    private static ProjectBuildAnalysisService CreateService()
    {
        return new ProjectBuildAnalysisService();
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }

    private static async Task<string> WriteProjectFileAsync(string directory, string projectName, string contents)
    {
        var projectPath = Path.Combine(directory, $"{projectName}.csproj");
        await File.WriteAllTextAsync(projectPath, contents);
        return projectPath;
    }

    private static Project CreateProject(string name, string projectPath)
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            name,
            name,
            LanguageNames.CSharp,
            filePath: projectPath);

        return workspace.AddProject(projectInfo);
    }

    private static void AssertDecision(
        ProjectBuildMetadataModel metadata,
        string decisionKind,
        string value,
        string ruleId,
        RuleConfidence confidence,
        params (string Source, string Key, string Value)[] expectedEvidence)
    {
        var decision = Assert.Single(metadata.RuleDecisions, x => x.RuleId == ruleId);
        Assert.Equal(decisionKind, decision.DecisionKind);
        Assert.Equal(value, decision.Value);
        Assert.Equal(ruleId, decision.RuleId);
        Assert.Equal("1.0", decision.RuleVersion);
        Assert.Equal(confidence, decision.Confidence);

        foreach (var evidence in expectedEvidence)
        {
            Assert.Contains(decision.Evidence, item =>
                item.Source == evidence.Source &&
                item.Key == evidence.Key &&
                item.Value == evidence.Value);
        }
    }
}
