using System.Text.RegularExpressions;
using TestMap.Models.Code;
using TestMap.Models.Results;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Services.TestExecution;

namespace TestMap.Rules.TestExecution;

internal static class TestExecutionDecisionEngine
{
    private static readonly List<FailureClassificationRule> FailureRules =
    [
        new(
            TestExecutionRuleDefinitions.FailureDockerEngine,
            "infrastructure",
            "docker_engine_failure",
            "Docker failed before the project build or test workflow could start.",
            "Verify the Docker daemon/context is available and that the selected container context is running.",
            0.98,
            "docker",
            [
                "Cannot connect to the Docker daemon",
                "error during connect",
                "The system cannot find the file specified",
                "no such host"
            ]),
        new(
            TestExecutionRuleDefinitions.FailurePackageRestore,
            "restore",
            "package_restore_failure",
            "NuGet package restore failed.",
            "Check package sources, feed credentials, network access, and whether the package/version exists for the selected target framework.",
            0.97,
            "restore",
            [
                "NU1101",
                "NU1102",
                "NU1301",
                "NU1302",
                "NU1303",
                "NU1304",
                "Unable to find package",
                "Unable to load the service index",
                "Response status code does not indicate success: 401",
                "Response status code does not indicate success: 403"
            ]),
        new(
            TestExecutionRuleDefinitions.FailureImageDependencyMissing,
            "build",
            "image_dependency_missing",
            "The container image is missing a required build dependency.",
            "Add the missing tool or runtime to the image. For Mono-based projects, install Mono/MSBuild/NuGet support in the container image.",
            0.95,
            "image",
            [
                "mono: command not found",
                "mono: not found",
                "msbuild: command not found",
                "xbuild: command not found",
                "nuget: command not found",
                "No such file or directory: mono"
            ]),
        new(
            TestExecutionRuleDefinitions.FailurePlatformMismatch,
            "build",
            "platform_mismatch",
            "The project requires Windows-specific targeting or workloads that are not available in the current container.",
            "Run the project in a Windows container or enable the required Windows targeting packs/workloads for the selected SDK image.",
            0.96,
            "build",
            [
                "NETSDK1100",
                "NETSDK1136",
                "WindowsDesktop",
                "UseWPF",
                "UseWindowsForms",
                "Microsoft.NET.Sdk.WindowsDesktop"
            ]),
        new(
            TestExecutionRuleDefinitions.FailureSdkOrWorkloadMissing,
            "build",
            "sdk_or_workload_missing",
            "The required .NET SDK, workload, or imported build targets are missing.",
            "Install the matching SDK/workload in the image or align the image SDK with the project's global.json and imported targets.",
            0.95,
            "build",
            [
                "MSB4019",
                "NETSDK1147",
                "The SDK '",
                "Workload installation failed",
                "was not found. Install the",
                "It was not possible to find any installed .NET SDKs"
            ]),
        new(
            TestExecutionRuleDefinitions.FailureCompilation,
            "build",
            "compilation_failure",
            "The project restored, but compilation failed.",
            "Inspect the compiler errors and project references. The source or target framework configuration likely needs to be fixed before tests can run.",
            0.9,
            "build",
            [
                "Build FAILED.",
                ": error CS",
                ": error MSB",
                ": error NU",
                "CSC : error"
            ]),
        new(
            TestExecutionRuleDefinitions.FailureTestExecution,
            "test",
            "test_execution_failure",
            "The build completed, but test execution failed before producing usable results.",
            "Inspect test host startup, adapter availability, and test framework configuration inside the container.",
            0.88,
            "test",
            [
                "Test Run Aborted",
                "The active test run was aborted",
                "No test is available",
                "testhost",
                "Could not find testhost"
            ]),
        new(
            TestExecutionRuleDefinitions.FailureCoverageCollection,
            "coverage",
            "coverage_collection_failure",
            "Coverage collection failed inside the container.",
            "Ensure the configured collector is installed and compatible with the project. For cross-platform runs, prefer the XPlat/Coverlet collector when available.",
            0.9,
            "coverage",
            [
                "Could not find data collector",
                "XPlat Code Coverage",
                "Unable to find a datacollector with friendly name",
                "coverage"
            ])
    ];

    public static RuleDecisionRecord DecideDockerContext(
        string configuredContext,
        bool requiresWindows,
        DockerRuntimePathMapper pathMapper)
    {
        if (requiresWindows)
            return CreateDecision(
                "DockerContext",
                DockerRuntimePathMapper.WindowsContextName,
                TestExecutionRuleDefinitions.DockerContextWindowsRequired,
                RuleConfidence.High,
                [CreateEvidence("BuildMetadata", "RequiresWindows", bool.TrueString, string.Empty)],
                "Windows is required, so the Windows Docker context is selected.");

        if (string.IsNullOrWhiteSpace(configuredContext) ||
            pathMapper.IsWindowsContext(configuredContext))
            return CreateDecision(
                "DockerContext",
                DockerRuntimePathMapper.LinuxContextName,
                TestExecutionRuleDefinitions.DockerContextDefaultLinux,
                RuleConfidence.High,
                [CreateEvidence("RuntimeConfig", "Docker.Context", configuredContext, string.Empty)],
                "Windows is not required, so blank or stale Windows context falls back to Linux.");

        return CreateDecision(
            "DockerContext",
            configuredContext,
            TestExecutionRuleDefinitions.DockerContextConfigured,
            RuleConfidence.High,
            [CreateEvidence("RuntimeConfig", "Docker.Context", configuredContext, string.Empty)],
            "Windows is not required, so the configured non-Windows Docker context is preserved.");
    }

    public static RuleDecisionRecord DecideSolutionWindowsRequirement(IEnumerable<CSharpProjectModel> solutionProjects)
    {
        var requiringProjects = solutionProjects
            .Where(project => project.BuildMetadata.WindowsRequirement is WindowsRequirementType.Required
                or WindowsRequirementType.LikelyRequired)
            .ToList();

        if (requiringProjects.Count > 0)
            return CreateDecision(
                "RequiresWindows",
                bool.TrueString,
                TestExecutionRuleDefinitions.WindowsRequiredByProject,
                RuleConfidence.High,
                requiringProjects.Select(project => CreateEvidence(
                    "ProjectBuildMetadata",
                    "WindowsRequirement",
                    project.BuildMetadata.WindowsRequirement.ToString(),
                    project.FilePath)).ToList(),
                "At least one project requires or likely requires Windows.");

        return CreateDecision(
            "RequiresWindows",
            bool.FalseString,
            TestExecutionRuleDefinitions.WindowsNotRequiredByProjects,
            RuleConfidence.High,
            solutionProjects.Select(project => CreateEvidence(
                "ProjectBuildMetadata",
                "WindowsRequirement",
                project.BuildMetadata.WindowsRequirement.ToString(),
                project.FilePath)).ToList(),
            "No project requires or likely requires Windows.");
    }

    public static RuleDecisionRecord DecideCommonBaselineTestFramework(IEnumerable<CSharpProjectModel> solutionProjects)
    {
        var testProjects = solutionProjects
            .Where(project => project.BuildMetadata.IsTestProject)
            .ToList();

        if (testProjects.Count == 0)
            return CreateDecision(
                "BaselineTargetFramework",
                string.Empty,
                TestExecutionRuleDefinitions.BaselineFrameworkNoTestProjects,
                RuleConfidence.High,
                [],
                "No test projects were discovered.");

        var commonFrameworks = new HashSet<string>(
            GetProjectTargetFrameworks(testProjects[0]),
            StringComparer.OrdinalIgnoreCase);

        foreach (var testProject in testProjects.Skip(1))
            commonFrameworks.IntersectWith(GetProjectTargetFrameworks(testProject));

        var selectedFramework = commonFrameworks
            .Select(ParseFrameworkPreference)
            .OrderByDescending(framework => framework.IsModernNet)
            .ThenBy(framework => framework.IsLegacyFramework)
            .ThenByDescending(framework => framework.Major)
            .ThenByDescending(framework => framework.Minor)
            .ThenByDescending(framework => framework.Framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            .Select(framework => framework.Framework)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(selectedFramework))
            return CreateDecision(
                "BaselineTargetFramework",
                string.Empty,
                TestExecutionRuleDefinitions.BaselineFrameworkNoSharedTarget,
                RuleConfidence.High,
                testProjects.Select(project => CreateEvidence(
                    "ProjectBuildMetadata",
                    "BuildTargets",
                    string.Join(";", GetProjectTargetFrameworks(project)),
                    project.FilePath)).ToList(),
                "No common target framework exists across discovered test projects.");

        return CreateDecision(
            "BaselineTargetFramework",
            selectedFramework,
            TestExecutionRuleDefinitions.BaselineFrameworkSharedTarget,
            RuleConfidence.High,
            testProjects.Select(project => CreateEvidence(
                "ProjectBuildMetadata",
                "BuildTargets",
                string.Join(";", GetProjectTargetFrameworks(project)),
                project.FilePath)).ToList(),
            "Selected the highest-preference target framework shared by all discovered test projects.");
    }

    public static RuleDecisionRecord DecidePreferredTargetFramework(CSharpProjectModel project)
    {
        var selectedFramework = GetProjectTargetFrameworks(project)
            .Select(ParseFrameworkPreference)
            .OrderByDescending(x => x.IsModernNet)
            .ThenBy(x => x.IsLegacyFramework)
            .ThenByDescending(x => x.Major)
            .ThenByDescending(x => x.Minor)
            .ThenByDescending(x => x.Framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Framework)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(selectedFramework))
            return CreateDecision(
                "TargetFramework",
                string.Empty,
                TestExecutionRuleDefinitions.TargetFrameworkMissing,
                RuleConfidence.Low,
                [CreateEvidence("ProjectBuildMetadata", "BuildTargets", string.Empty, project.FilePath)],
                "No target framework was available for the selected project.");

        return CreateDecision(
            "TargetFramework",
            selectedFramework,
            TestExecutionRuleDefinitions.TargetFrameworkPreferred,
            RuleConfidence.High,
            [CreateEvidence("ProjectBuildMetadata", "BuildTargets", string.Join(";", GetProjectTargetFrameworks(project)), project.FilePath)],
            "Selected the highest-preference target framework for the selected project.");
    }

    public static RuleDecisionRecord DecideCoverageCollectorArgument(CoverageCollectorType collectorType)
    {
        if (collectorType == CoverageCollectorType.Coverlet)
            return CreateDecision(
                "CoverageCollectorArgument",
                "XPlat Code Coverage",
                TestExecutionRuleDefinitions.CoverageCollectorCoverlet,
                RuleConfidence.High,
                [CreateEvidence("ProjectBuildMetadata", "CoverageCollector", collectorType.ToString(), string.Empty)],
                "Coverlet coverage metadata maps to XPlat Code Coverage.");

        return CreateDecision(
            "CoverageCollectorArgument",
            "Code Coverage;Format=Cobertura",
            TestExecutionRuleDefinitions.CoverageCollectorMicrosoft,
            collectorType == CoverageCollectorType.MicrosoftCodeCoverage ? RuleConfidence.High : RuleConfidence.Medium,
            [CreateEvidence("ProjectBuildMetadata", "CoverageCollector", collectorType.ToString(), string.Empty)],
            "Microsoft or unknown coverage metadata maps to Code Coverage with Cobertura output.");
    }

    public static FailureAnalysisModel? DecideFailureAnalysis(string? logs, string? processDiagnostics = null)
    {
        var combined = string.Join(
            Environment.NewLine,
            new[] { logs, processDiagnostics }.Where(x => !string.IsNullOrWhiteSpace(x)));

        if (string.IsNullOrWhiteSpace(combined)) return null;

        foreach (var rule in FailureRules)
        {
            var matched = rule.Patterns
                .Where(pattern => combined.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matched.Count == 0) continue;

            var evidence = ExtractEvidence(combined, matched[0]);
            var decision = CreateDecision(
                "FailureClassification",
                rule.Category,
                rule.Rule,
                ToRuleConfidence(rule.Confidence),
                matched.Select(pattern => CreateEvidence(rule.Source, "MatchedPattern", pattern, evidence)).ToList(),
                rule.Summary);

            return new FailureAnalysisModel
            {
                Stage = rule.Stage,
                Category = rule.Category,
                Summary = rule.Summary,
                RemediationSuggestion = rule.Remediation,
                Confidence = rule.Confidence,
                Source = rule.Source,
                MatchedPatterns = matched,
                Evidence = evidence,
                RuleDecision = decision
            };
        }

        var fallbackEvidence = ExtractEvidence(combined, "error");
        return new FailureAnalysisModel
        {
            Stage = "unknown",
            Category = "unknown_failure",
            Summary = "The container run failed, but no known failure pattern matched the available diagnostics.",
            RemediationSuggestion =
                "Inspect the stored docker log and add a classifier rule for this failure pattern if it is recurring.",
            Confidence = 0.25,
            Source = "unknown",
            MatchedPatterns = new List<string>(),
            Evidence = fallbackEvidence,
            RuleDecision = CreateDecision(
                "FailureClassification",
                "unknown_failure",
                TestExecutionRuleDefinitions.FailureUnknown,
                RuleConfidence.Low,
                [CreateEvidence("Diagnostics", "FallbackEvidence", fallbackEvidence, string.Empty)],
                "No known failure classification rule matched.")
        };
    }

    public static RuleDecisionRecord DecideContainerPath(
        string hostPath,
        string projectDirectory,
        string dockerContext)
    {
        var projectRoot = Path.GetFullPath(projectDirectory);
        var fullPath = Path.GetFullPath(hostPath);
        var relativePath = TryGetRelativePathInsideProject(fullPath, projectRoot);

        if (relativePath == null)
            return CreateDecision(
                "ContainerPath",
                string.Empty,
                TestExecutionRuleDefinitions.ContainerPathOutsideProject,
                RuleConfidence.High,
                [
                    CreateEvidence("Filesystem", "HostPath", fullPath, fullPath),
                    CreateEvidence("Filesystem", "ProjectRoot", projectRoot, projectRoot)
                ],
                "The host path is outside the mounted project directory.");

        var isWindowsContext = IsWindowsContext(dockerContext);
        var containerPath = isWindowsContext
            ? $@"{DockerRuntimePathMapper.WindowsProjectRoot}\{relativePath.Replace('/', '\\')}"
            : $"{DockerRuntimePathMapper.LinuxProjectRoot}/{relativePath.Replace('\\', '/')}";

        return CreateDecision(
            "ContainerPath",
            containerPath,
            isWindowsContext
                ? TestExecutionRuleDefinitions.ContainerPathWindows
                : TestExecutionRuleDefinitions.ContainerPathLinux,
            RuleConfidence.High,
            [
                CreateEvidence("Filesystem", "HostPath", fullPath, fullPath),
                CreateEvidence("RuntimeConfig", "Docker.Context", dockerContext, string.Empty)
            ],
            "Mapped a project-relative host path into the selected container filesystem.");
    }

    public static RuleDecisionRecord DecideMountArgument(string projectDirectory, string dockerContext)
    {
        var isWindowsContext = IsWindowsContext(dockerContext);
        var containerRoot = isWindowsContext
            ? DockerRuntimePathMapper.WindowsProjectRoot
            : DockerRuntimePathMapper.LinuxProjectRoot;

        return CreateDecision(
            "MountArgument",
            $"-v \"{projectDirectory}:{containerRoot}\"",
            isWindowsContext
                ? TestExecutionRuleDefinitions.MountArgumentWindows
                : TestExecutionRuleDefinitions.MountArgumentLinux,
            RuleConfidence.High,
            [CreateEvidence("RuntimeConfig", "Docker.Context", dockerContext, string.Empty)],
            "Mapped the project directory to the selected container project root.");
    }

    public static RuleDecisionRecord DecideExpectedOs(string dockerContext)
    {
        var isWindowsContext = IsWindowsContext(dockerContext);
        return CreateDecision(
            "ExpectedDockerOs",
            isWindowsContext ? "windows" : "linux",
            isWindowsContext
                ? TestExecutionRuleDefinitions.ExpectedOsWindows
                : TestExecutionRuleDefinitions.ExpectedOsLinux,
            RuleConfidence.High,
            [CreateEvidence("RuntimeConfig", "Docker.Context", dockerContext, string.Empty)],
            "Selected the expected Docker daemon OS from the Docker context.");
    }

    public static RuleDecisionRecord DecideArtifactCleanup(bool preserveArtifacts)
    {
        return CreateDecision(
            "ArtifactCleanup",
            preserveArtifacts ? "Preserve" : "Delete",
            preserveArtifacts
                ? TestExecutionRuleDefinitions.CleanupPreserveArtifacts
                : TestExecutionRuleDefinitions.CleanupDeleteArtifacts,
            RuleConfidence.High,
            [CreateEvidence("RunState", "PreserveArtifacts", preserveArtifacts.ToString(), string.Empty)],
            preserveArtifacts
                ? "Artifacts are preserved for failed run diagnostics."
                : "Generated test execution artifacts are deleted.");
    }

    private static List<string> GetProjectTargetFrameworks(CSharpProjectModel project)
    {
        return (project.BuildMetadata.BuildTargets.Count > 0
                ? project.BuildMetadata.BuildTargets
                : project.BuildTargets)
            .Where(framework => !string.IsNullOrWhiteSpace(framework))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static FrameworkPreference ParseFrameworkPreference(string framework)
    {
        var legacyMatch = Regex.Match(framework, @"^net(?<major>[1-4])(?<minor>\d)(?<patch>\d)?$",
            RegexOptions.IgnoreCase);
        if (legacyMatch.Success)
        {
            var legacyMajor = int.TryParse(legacyMatch.Groups["major"].Value, out var parsedLegacyMajor)
                ? parsedLegacyMajor
                : -1;
            var legacyMinor = int.TryParse(legacyMatch.Groups["minor"].Value, out var parsedLegacyMinor)
                ? parsedLegacyMinor
                : 0;
            return new FrameworkPreference(framework, false, true, legacyMajor, legacyMinor);
        }

        var modernMatch = Regex.Match(framework, @"^net(?<major>[5-9]|\d{2,})(?:\.(?<minor>\d+))?$",
            RegexOptions.IgnoreCase);
        if (modernMatch.Success)
        {
            var modernMajor = int.TryParse(modernMatch.Groups["major"].Value, out var parsedModernMajor)
                ? parsedModernMajor
                : -1;
            var modernMinor = int.TryParse(modernMatch.Groups["minor"].Value, out var parsedModernMinor)
                ? parsedModernMinor
                : 0;
            return new FrameworkPreference(framework, true, false, modernMajor, modernMinor);
        }

        var netStandardOrCoreMatch = Regex.Match(framework,
            @"^net(?:standard|coreapp)(?<major>\d+)(?:\.(?<minor>\d+))?$", RegexOptions.IgnoreCase);
        if (netStandardOrCoreMatch.Success)
        {
            var major = int.TryParse(netStandardOrCoreMatch.Groups["major"].Value, out var parsedMajor)
                ? parsedMajor
                : -1;
            var minor = int.TryParse(netStandardOrCoreMatch.Groups["minor"].Value, out var parsedMinor)
                ? parsedMinor
                : 0;
            return new FrameworkPreference(framework, false, false, major, minor);
        }

        if (!framework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            return new FrameworkPreference(framework, false, false, -1, -1);

        return new FrameworkPreference(framework, false, false, -1, -1);
    }

    private static RuleDecisionRecord CreateDecision(
        string decisionKind,
        string value,
        RuleDefinition rule,
        RuleConfidence confidence,
        List<RuleEvidenceRecord> evidence,
        string notes)
    {
        return RuleDecisionFactory.CreateDecision(
            decisionKind,
            value,
            rule,
            confidence,
            evidence,
            notes);
    }

    private static RuleEvidenceRecord CreateEvidence(string source, string key, string value, string location)
    {
        return RuleDecisionFactory.CreateEvidence(source, key, value, location);
    }

    private static string? TryGetRelativePathInsideProject(string fullPath, string projectRoot)
    {
        var relativePath = Path.GetRelativePath(projectRoot, fullPath);
        if (relativePath == ".") return string.Empty;
        if (Path.IsPathFullyQualified(relativePath) ||
            relativePath.StartsWith("..", StringComparison.Ordinal) &&
            (relativePath.Length == 2 || Path.DirectorySeparatorChar == relativePath[2] || Path.AltDirectorySeparatorChar == relativePath[2]))
            return null;

        return relativePath;
    }

    private static string ExtractEvidence(string content, string pattern)
    {
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var matchedLine = lines.FirstOrDefault(line => line.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(matchedLine)) return matchedLine.Trim();

        var generic = lines.FirstOrDefault(line =>
            Regex.IsMatch(line, "error|failed|abort|exception", RegexOptions.IgnoreCase));
        return generic?.Trim() ?? string.Empty;
    }

    private static RuleConfidence ToRuleConfidence(double confidence)
    {
        return confidence switch
        {
            >= 0.9 => RuleConfidence.High,
            >= 0.6 => RuleConfidence.Medium,
            > 0 => RuleConfidence.Low,
            _ => RuleConfidence.Unknown
        };
    }

    private static bool IsWindowsContext(string dockerContext)
    {
        return dockerContext.Contains(DockerRuntimePathMapper.WindowsContextName, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record FailureClassificationRule(
        RuleDefinition Rule,
        string Stage,
        string Category,
        string Summary,
        string Remediation,
        double Confidence,
        string Source,
        List<string> Patterns);

    private sealed record FrameworkPreference(
        string Framework,
        bool IsModernNet,
        bool IsLegacyFramework,
        int Major,
        int Minor);
}
