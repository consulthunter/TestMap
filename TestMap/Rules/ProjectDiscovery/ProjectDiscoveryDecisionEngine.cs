using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using TestMap.Models.Code;
using TestMap.Models.Rules;
using TestMap.Rules;

namespace TestMap.Rules.ProjectDiscovery;

internal static class ProjectDiscoveryDecisionEngine
{
    private static readonly HashSet<string> UnsupportedWorkloadPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "android",
        "browser",
        "ios",
        "maccatalyst",
        "macos",
        "tvos",
        "wasm"
    };

    private static readonly string[] TestPackageMarkers =
    [
        "microsoft.net.test.sdk",
        "xunit",
        "nunit",
        "mstest.testframework",
        "mstest"
    ];

    public static RuleDecisionRecord CreateMissingProjectFileDecision()
    {
        return CreateDecision(
            "ProjectFile",
            "Missing",
            ProjectDiscoveryRuleDefinitions.ProjectFileMissing,
            RuleConfidence.High,
            [],
            "Project file path is missing or does not exist.");
    }

    public static RuleDecisionRecord CreateProjectFileDecision(ProjectFileAnalysis projectFile, string projectFilePath)
    {
        return CreateDecision(
            "ProjectFile",
            projectFile.ParseSucceeded ? "Parsed" : "ParseFailed",
            projectFile.ParseSucceeded
                ? ProjectDiscoveryRuleDefinitions.ProjectFileParsed
                : ProjectDiscoveryRuleDefinitions.ProjectFileParseFailed,
            RuleConfidence.High,
            [CreateEvidence("ProjectXml", "FilePath", projectFilePath, projectFilePath)],
            projectFile.ParseSucceeded
                ? "Project file XML was parsed successfully."
                : "Project file XML could not be parsed; downstream project discovery decisions use safe defaults.");
    }

    public static RuleDecisionRecord SelectBuildTargets(ProjectFileAnalysis projectFile, string projectFilePath)
    {
        if (!projectFile.ParseSucceeded)
            return CreateDecision(
                "BuildTargets",
                string.Empty,
                ProjectDiscoveryRuleDefinitions.BuildTargetsUnavailable,
                RuleConfidence.Unknown,
                [CreateEvidence("ProjectXml", "ParseSucceeded", "false", projectFilePath)],
                "Build targets are unknown because project XML could not be parsed.");

        var targetFramework = projectFile.GetProperty("TargetFramework");
        if (!string.IsNullOrWhiteSpace(targetFramework))
            return CreateDecision(
                "BuildTargets",
                targetFramework,
                ProjectDiscoveryRuleDefinitions.BuildTargetsFromTargetFramework,
                RuleConfidence.Medium,
                [CreateEvidence("ProjectXml", "TargetFramework", targetFramework, projectFilePath)],
                "TargetFramework takes precedence over TargetFrameworks.");

        var targetFrameworksRaw = projectFile.GetProperty("TargetFrameworks");
        var targetFrameworks = SplitMultiValue(targetFrameworksRaw);
        if (targetFrameworks.Count > 0)
            return CreateDecision(
                "BuildTargets",
                string.Join(";", targetFrameworks),
                ProjectDiscoveryRuleDefinitions.BuildTargetsFromTargetFrameworks,
                RuleConfidence.Medium,
                [CreateEvidence("ProjectXml", "TargetFrameworks", targetFrameworksRaw, projectFilePath)],
                "Target frameworks were read from TargetFrameworks.");

        var targetFrameworkVersion = projectFile.GetProperty("TargetFrameworkVersion");
        var normalizedTargetFrameworkVersion = NormalizeTargetFrameworkVersion(targetFrameworkVersion);
        if (!string.IsNullOrWhiteSpace(normalizedTargetFrameworkVersion))
            return CreateDecision(
                "BuildTargets",
                normalizedTargetFrameworkVersion,
                ProjectDiscoveryRuleDefinitions.BuildTargetsFromTargetFrameworkVersion,
                RuleConfidence.Medium,
                [CreateEvidence("ProjectXml", "TargetFrameworkVersion", targetFrameworkVersion, projectFilePath)],
                "Target framework was normalized from legacy TargetFrameworkVersion.");

        return CreateDecision(
            "BuildTargets",
            string.Empty,
            ProjectDiscoveryRuleDefinitions.BuildTargetsMissing,
            RuleConfidence.Low,
            [],
            "No TargetFramework or TargetFrameworks value was found in project XML.");
    }

    public static RuleDecisionRecord CreateSdkDecision(string sdkVersion, string? globalJsonPath)
    {
        if (string.IsNullOrWhiteSpace(globalJsonPath))
            return CreateDecision(
                "SdkVersion",
                string.Empty,
                ProjectDiscoveryRuleDefinitions.SdkVersionMissingGlobalJson,
                RuleConfidence.Low,
                [],
                "No nearest global.json file was found.");

        return CreateDecision(
            "SdkVersion",
            sdkVersion,
            string.IsNullOrWhiteSpace(sdkVersion)
                ? ProjectDiscoveryRuleDefinitions.SdkVersionUnreadableGlobalJson
                : ProjectDiscoveryRuleDefinitions.SdkVersionFromGlobalJson,
            string.IsNullOrWhiteSpace(sdkVersion) ? RuleConfidence.Low : RuleConfidence.Medium,
            [
                CreateEvidence("GlobalJson", "Path", globalJsonPath, globalJsonPath),
                CreateEvidence("GlobalJson", "sdk.version", sdkVersion, globalJsonPath)
            ],
            string.IsNullOrWhiteSpace(sdkVersion)
                ? "global.json was found, but no SDK version could be read."
                : "SDK version was read from nearest global.json.");
    }

    public static List<RuleDecisionRecord> DetectTestProject(
        Project project,
        List<string> packageReferences,
        ProjectFileAnalysis projectFile)
    {
        var decisions = new List<RuleDecisionRecord>();

        if (IsTrue(projectFile.GetProperty("IsTestProject")))
            decisions.Add(CreateDecision(
                "IsTestProject",
                bool.TrueString,
                ProjectDiscoveryRuleDefinitions.TestProjectExplicit,
                RuleConfidence.High,
                [CreateEvidence("ProjectXml", "IsTestProject", projectFile.GetProperty("IsTestProject"), project.FilePath ?? string.Empty)],
                "Project explicitly sets IsTestProject=true."));

        if (project.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CreateDecision(
                "IsTestProject",
                bool.TrueString,
                ProjectDiscoveryRuleDefinitions.TestProjectByName,
                RuleConfidence.Medium,
                [CreateEvidence("RoslynProject", "Name", project.Name, project.FilePath ?? string.Empty)],
                "Project name ends with .Tests."));

        var matchingPackages = packageReferences
            .Where(x => TestPackageMarkers.Any(marker => x.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (matchingPackages.Count > 0)
            decisions.Add(CreateDecision(
                "IsTestProject",
                bool.TrueString,
                ProjectDiscoveryRuleDefinitions.TestProjectByPackage,
                RuleConfidence.High,
                matchingPackages.Select(package =>
                    CreateEvidence("ProjectXml", "PackageReference", package, project.FilePath ?? string.Empty)).ToList(),
                "Project references known test packages."));

        if (decisions.Count > 0) return decisions;

        decisions.Add(CreateDecision(
            "IsTestProject",
            bool.FalseString,
            ProjectDiscoveryRuleDefinitions.TestProjectNoSignal,
            RuleConfidence.Low,
            [],
            "No explicit test project signal, test-style project name, or known test package was found."));
        return decisions;
    }

    public static List<RuleDecisionRecord> DetermineWindowsRequirement(
        List<string> buildTargets,
        string runtimeIdentifier,
        List<string> runtimeIdentifiers,
        string targetPlatformIdentifier,
        bool usesWindowsDesktop,
        List<string> packageReferences,
        string projectFilePath)
    {
        var decisions = new List<RuleDecisionRecord>();
        if (usesWindowsDesktop)
            decisions.Add(CreateDecision(
                "WindowsRequirement",
                WindowsRequirementType.Required.ToString(),
                ProjectDiscoveryRuleDefinitions.WindowsRequiredByDesktop,
                RuleConfidence.High,
                [CreateEvidence("ProjectXml", "WindowsDesktop", "UseWPF/UseWindowsForms", projectFilePath)],
                "Windows is required by Windows desktop settings."));
        if (string.Equals(targetPlatformIdentifier, "Windows", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CreateDecision(
                "WindowsRequirement",
                WindowsRequirementType.Required.ToString(),
                ProjectDiscoveryRuleDefinitions.WindowsRequiredByTargetPlatform,
                RuleConfidence.High,
                [CreateEvidence("ProjectXml", "TargetPlatformIdentifier", targetPlatformIdentifier, projectFilePath)],
                "Windows is required by the Windows target platform."));
        decisions.AddRange(buildTargets
            .Where(x => x.Contains("-windows", StringComparison.OrdinalIgnoreCase))
            .Select(x => CreateDecision(
                "WindowsRequirement",
                WindowsRequirementType.Required.ToString(),
                ProjectDiscoveryRuleDefinitions.WindowsRequiredByWindowsTargetFramework,
                RuleConfidence.High,
                [CreateEvidence("ProjectXml", "TargetFramework", x, projectFilePath)],
                "Windows is required by a Windows target framework.")));
        decisions.AddRange(buildTargets
            .Where(IsLegacyNetFrameworkTarget)
            .Select(x => CreateDecision(
                "WindowsRequirement",
                WindowsRequirementType.Required.ToString(),
                ProjectDiscoveryRuleDefinitions.WindowsRequiredByLegacyNetFramework,
                RuleConfidence.High,
                [CreateEvidence("ProjectXml", "TargetFramework", x, projectFilePath)],
                "Windows is required by legacy .NET Framework targeting.")));

        if (!string.IsNullOrWhiteSpace(runtimeIdentifier) &&
            runtimeIdentifier.StartsWith("win", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CreateDecision(
                "WindowsRequirement",
                WindowsRequirementType.LikelyRequired.ToString(),
                ProjectDiscoveryRuleDefinitions.WindowsLikelyByRuntimeIdentifier,
                RuleConfidence.Medium,
                [CreateEvidence("ProjectXml", "RuntimeIdentifier", runtimeIdentifier, projectFilePath)],
                "Windows is likely required by RuntimeIdentifier."));
        decisions.AddRange(runtimeIdentifiers
            .Where(x => x.StartsWith("win", StringComparison.OrdinalIgnoreCase))
            .Select(x => CreateDecision(
                "WindowsRequirement",
                WindowsRequirementType.LikelyRequired.ToString(),
                ProjectDiscoveryRuleDefinitions.WindowsLikelyByRuntimeIdentifiers,
                RuleConfidence.Medium,
                [CreateEvidence("ProjectXml", "RuntimeIdentifiers", x, projectFilePath)],
                "Windows is likely required by RuntimeIdentifiers.")));
        decisions.AddRange(packageReferences
            .Where(x =>
                x.Contains("windowsdesktop", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("microsoft.windows", StringComparison.OrdinalIgnoreCase))
            .Select(x => CreateDecision(
                "WindowsRequirement",
                WindowsRequirementType.LikelyRequired.ToString(),
                ProjectDiscoveryRuleDefinitions.WindowsLikelyByPackage,
                RuleConfidence.Medium,
                [CreateEvidence("ProjectXml", "PackageReference", x, projectFilePath)],
                "Windows is likely required by a Windows-oriented package reference.")));

        if (decisions.Count > 0) return decisions;

        decisions.Add(CreateDecision(
            "WindowsRequirement",
            buildTargets.Count == 0 ? WindowsRequirementType.Unknown.ToString() : WindowsRequirementType.NotRequired.ToString(),
            buildTargets.Count == 0
                ? ProjectDiscoveryRuleDefinitions.WindowsUnknownNoBuildTargets
                : ProjectDiscoveryRuleDefinitions.WindowsNotRequiredNoSignal,
            buildTargets.Count == 0 ? RuleConfidence.Unknown : RuleConfidence.Low,
            buildTargets.Select(x => CreateEvidence("ProjectXml", "TargetFramework", x, projectFilePath)).ToList(),
            buildTargets.Count == 0
                ? "Windows requirement is unknown because no build target was discovered."
                : "No Windows-specific project discovery signal was found."));
        return decisions;
    }

    public static List<RuleDecisionRecord> DetermineExecutionSupport(
        List<string> buildTargets,
        string targetPlatformIdentifier,
        string projectFilePath)
    {
        var decisions = new List<RuleDecisionRecord>();
        decisions.AddRange(buildTargets
            .Select(target => (Target: target, Platform: TryGetTargetFrameworkPlatform(target)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Platform) &&
                        !x.Platform.Equals("windows", StringComparison.OrdinalIgnoreCase))
            .Select(x => CreateDecision(
                "ExecutionSupport",
                UnsupportedWorkloadPlatforms.Contains(x.Platform)
                    ? ExecutionSupportType.UnsupportedWorkload.ToString()
                    : ExecutionSupportType.UnsupportedPlatform.ToString(),
                ProjectDiscoveryRuleDefinitions.ExecutionSupportUnsupportedTargetFramework,
                RuleConfidence.High,
                [CreateEvidence("ProjectXml", "TargetFramework", x.Target, projectFilePath)],
                "A target framework requires a non-Windows platform workload that the generic runner does not support.")));

        if (!string.IsNullOrWhiteSpace(targetPlatformIdentifier) &&
            !targetPlatformIdentifier.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            decisions.Add(CreateDecision(
                "ExecutionSupport",
                UnsupportedWorkloadPlatforms.Contains(targetPlatformIdentifier)
                    ? ExecutionSupportType.UnsupportedWorkload.ToString()
                    : ExecutionSupportType.UnsupportedPlatform.ToString(),
                ProjectDiscoveryRuleDefinitions.ExecutionSupportUnsupportedTargetPlatform,
                RuleConfidence.High,
                [CreateEvidence("ProjectXml", "TargetPlatformIdentifier", targetPlatformIdentifier, projectFilePath)],
                "TargetPlatformIdentifier names a platform that the generic runner does not support."));

        if (decisions.Count > 0) return decisions;

        decisions.Add(CreateDecision(
            "ExecutionSupport",
            buildTargets.Count == 0 ? ExecutionSupportType.Unknown.ToString() : ExecutionSupportType.Supported.ToString(),
            buildTargets.Count == 0
                ? ProjectDiscoveryRuleDefinitions.ExecutionSupportUnknownNoBuildTargets
                : ProjectDiscoveryRuleDefinitions.ExecutionSupportSupported,
            buildTargets.Count == 0 ? RuleConfidence.Unknown : RuleConfidence.Low,
            buildTargets.Select(x => CreateEvidence("ProjectXml", "TargetFramework", x, projectFilePath)).ToList(),
            buildTargets.Count == 0
                ? "Execution support is unknown because no build target was discovered."
                : "No unsupported platform or workload target was found."));
        return decisions;
    }

    public static List<RuleDecisionRecord> DetermineCoverageCollector(List<string> packageReferences, string projectFilePath)
    {
        var decisions = new List<RuleDecisionRecord>();

        decisions.AddRange(packageReferences
            .Where(x => x.Equals("coverlet.collector", StringComparison.OrdinalIgnoreCase))
            .Select(coverletPackage => CreateDecision(
                "CoverageCollector",
                CoverageCollectorType.Coverlet.ToString(),
                ProjectDiscoveryRuleDefinitions.CoverageCollectorCoverlet,
                RuleConfidence.High,
                [CreateEvidence("ProjectXml", "PackageReference", coverletPackage, projectFilePath)],
                "Coverlet collector package was found.")));

        decisions.AddRange(packageReferences
            .Where(x => x.Equals("Microsoft.CodeCoverage", StringComparison.OrdinalIgnoreCase) ||
                        x.Equals("Microsoft.Testing.Extensions.CodeCoverage", StringComparison.OrdinalIgnoreCase))
            .Select(microsoftCoveragePackage => CreateDecision(
                "CoverageCollector",
                CoverageCollectorType.MicrosoftCodeCoverage.ToString(),
                ProjectDiscoveryRuleDefinitions.CoverageCollectorMicrosoft,
                RuleConfidence.High,
                [CreateEvidence("ProjectXml", "PackageReference", microsoftCoveragePackage, projectFilePath)],
                "Microsoft coverage package was found.")));

        if (decisions.Count > 0) return decisions;

        decisions.Add(CreateDecision(
            "CoverageCollector",
            CoverageCollectorType.Unknown.ToString(),
            ProjectDiscoveryRuleDefinitions.CoverageCollectorUnknown,
            RuleConfidence.Low,
            [],
            "No known coverage collector package was found."));
        return decisions;
    }

    public static WindowsRequirementType ResolveWindowsRequirement(List<RuleDecisionRecord> decisions)
    {
        if (decisions.Any(x => x.Value == WindowsRequirementType.Required.ToString()))
            return WindowsRequirementType.Required;
        if (decisions.Any(x => x.Value == WindowsRequirementType.LikelyRequired.ToString()))
            return WindowsRequirementType.LikelyRequired;
        if (decisions.Any(x => x.Value == WindowsRequirementType.NotRequired.ToString()))
            return WindowsRequirementType.NotRequired;
        return WindowsRequirementType.Unknown;
    }

    public static ExecutionSupportType ResolveExecutionSupport(List<RuleDecisionRecord> decisions)
    {
        if (decisions.Any(x => x.Value == ExecutionSupportType.UnsupportedWorkload.ToString()))
            return ExecutionSupportType.UnsupportedWorkload;
        if (decisions.Any(x => x.Value == ExecutionSupportType.UnsupportedPlatform.ToString()))
            return ExecutionSupportType.UnsupportedPlatform;
        if (decisions.Any(x => x.Value == ExecutionSupportType.Supported.ToString()))
            return ExecutionSupportType.Supported;
        return ExecutionSupportType.Unknown;
    }

    public static CoverageCollectorType ResolveCoverageCollector(List<RuleDecisionRecord> decisions)
    {
        if (decisions.Any(x => x.Value == CoverageCollectorType.Coverlet.ToString()))
            return CoverageCollectorType.Coverlet;
        if (decisions.Any(x => x.Value == CoverageCollectorType.MicrosoftCodeCoverage.ToString()))
            return CoverageCollectorType.MicrosoftCodeCoverage;
        return CoverageCollectorType.Unknown;
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

    private static List<string> SplitMultiValue(string value)
    {
        return value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeTargetFrameworkVersion(string value)
    {
        var match = Regex.Match(value.Trim(), @"^v?(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?$",
            RegexOptions.IgnoreCase);
        if (!match.Success) return string.Empty;

        var major = match.Groups["major"].Value;
        var minor = match.Groups["minor"].Success ? match.Groups["minor"].Value : "0";
        var patch = match.Groups["patch"].Success ? match.Groups["patch"].Value : string.Empty;

        return $"net{major}{minor}{patch}";
    }

    private static bool IsLegacyNetFrameworkTarget(string value)
    {
        return Regex.IsMatch(value.Trim(), @"^net[1-4]\d{1,2}$", RegexOptions.IgnoreCase);
    }

    private static string TryGetTargetFrameworkPlatform(string value)
    {
        var match = Regex.Match(value.Trim(), @"^net\d+(?:\.\d+)?-(?<platform>[a-z]+)",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["platform"].Value : string.Empty;
    }

    private static bool IsTrue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
