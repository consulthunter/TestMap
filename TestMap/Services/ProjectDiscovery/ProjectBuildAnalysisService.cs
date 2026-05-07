using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using TestMap.Models.Code;
using TestMap.Models.Rules;
using TestMap.Rules.ProjectDiscovery;

namespace TestMap.Services.ProjectDiscovery;

public class ProjectBuildAnalysisService : IProjectBuildAnalysisService
{
    public Task<ProjectBuildMetadataModel> AnalyzeAsync(Project project)
    {
        if (string.IsNullOrWhiteSpace(project.FilePath) || !File.Exists(project.FilePath))
            return Task.FromResult(new ProjectBuildMetadataModel
            {
                RuleDecisions = [ProjectDiscoveryDecisionEngine.CreateMissingProjectFileDecision()],
                Notes = "Project file path is missing."
            });

        var projectFile = ReadProjectFile(project.FilePath);
        var packageReferences = projectFile.PackageReferences;
        var nearestGlobalJson = FindNearestGlobalJson(project.FilePath);

        var buildTargetsDecision = ProjectDiscoveryDecisionEngine.SelectBuildTargets(projectFile, project.FilePath);
        var buildTargets = SplitMultiValue(buildTargetsDecision.Value);

        var runtimeIdentifier = projectFile.GetProperty("RuntimeIdentifier");
        var runtimeIdentifiers = SplitMultiValue(projectFile.GetProperty("RuntimeIdentifiers"));
        var targetPlatformIdentifier = projectFile.GetProperty("TargetPlatformIdentifier");
        var useWpf = IsTrue(projectFile.GetProperty("UseWPF"));
        var useWindowsForms = IsTrue(projectFile.GetProperty("UseWindowsForms"));
        var usesWindowsDesktop = useWpf || useWindowsForms;
        var testProjectDecisions = ProjectDiscoveryDecisionEngine.DetectTestProject(project, packageReferences, projectFile);
        var isTestProject = testProjectDecisions.Any(x => bool.Parse(x.Value));
        var windowsRequirementDecisions = ProjectDiscoveryDecisionEngine.DetermineWindowsRequirement(
            buildTargets,
            runtimeIdentifier,
            runtimeIdentifiers,
            targetPlatformIdentifier,
            usesWindowsDesktop,
            packageReferences,
            project.FilePath);
        var windowsRequirement = ProjectDiscoveryDecisionEngine.ResolveWindowsRequirement(windowsRequirementDecisions);
        var coverageCollectorDecisions =
            ProjectDiscoveryDecisionEngine.DetermineCoverageCollector(packageReferences, project.FilePath);
        var coverageCollector = ProjectDiscoveryDecisionEngine.ResolveCoverageCollector(coverageCollectorDecisions);
        var sdkVersion = ReadSdkVersion(nearestGlobalJson);
        var sdkDecision = ProjectDiscoveryDecisionEngine.CreateSdkDecision(sdkVersion, nearestGlobalJson);
        var decisions = new List<RuleDecisionRecord>
        {
            ProjectDiscoveryDecisionEngine.CreateProjectFileDecision(projectFile, project.FilePath),
            buildTargetsDecision,
            sdkDecision
        };
        decisions.AddRange(testProjectDecisions);
        decisions.AddRange(windowsRequirementDecisions);
        decisions.AddRange(coverageCollectorDecisions);

        return Task.FromResult(new ProjectBuildMetadataModel
        {
            BuildTargets = buildTargets,
            DefaultBuildTarget = buildTargets.FirstOrDefault() ?? string.Empty,
            GlobalJsonPath = nearestGlobalJson ?? string.Empty,
            SdkVersion = sdkVersion,
            RuntimeIdentifier = runtimeIdentifier,
            RuntimeIdentifiers = runtimeIdentifiers,
            TargetPlatformIdentifier = targetPlatformIdentifier,
            IsTestProject = isTestProject,
            UsesWindowsDesktop = usesWindowsDesktop,
            WindowsRequirement = windowsRequirement,
            CoverageCollector = coverageCollector,
            RuleDecisions = decisions,
            Notes = BuildNotes(sdkVersion, nearestGlobalJson, buildTargets, coverageCollector, windowsRequirement)
        });
    }

    private static ProjectFileAnalysis ReadProjectFile(string projectFilePath)
    {
        try
        {
            var doc = XDocument.Load(projectFilePath);
            var properties = doc.Descendants()
                .Where(x => x.Parent?.Name.LocalName == "PropertyGroup")
                .Where(x => !x.HasElements)
                .GroupBy(x => x.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Last().Value.Trim(),
                    StringComparer.OrdinalIgnoreCase);
            var packageReferences = doc.Descendants()
                .Where(x => x.Name.LocalName == "PackageReference")
                .Select(x => x.Attribute("Include")?.Value ?? x.Attribute("Update")?.Value ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ProjectFileAnalysis(true, properties, packageReferences);
        }
        catch
        {
            return ProjectFileAnalysis.Empty;
        }
    }

    private static string? FindNearestGlobalJson(string projectFilePath)
    {
        var directory = Path.GetDirectoryName(projectFilePath);

        while (!string.IsNullOrWhiteSpace(directory))
        {
            var candidate = Path.Combine(directory, "global.json");
            if (File.Exists(candidate)) return candidate;

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private static string ReadSdkVersion(string? globalJsonPath)
    {
        if (string.IsNullOrWhiteSpace(globalJsonPath) || !File.Exists(globalJsonPath)) return string.Empty;

        try
        {
            var globalJson = JObject.Parse(File.ReadAllText(globalJsonPath));
            return globalJson["sdk"]?["version"]?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static List<string> SplitMultiValue(string value)
    {
        return value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildNotes(
        string sdkVersion,
        string? globalJsonPath,
        List<string> buildTargets,
        CoverageCollectorType coverageCollector,
        WindowsRequirementType windowsRequirement)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(sdkVersion)) parts.Add($"SDK {sdkVersion}");

        if (!string.IsNullOrWhiteSpace(globalJsonPath)) parts.Add($"global.json: {globalJsonPath}");

        if (buildTargets.Count > 0) parts.Add($"targets: {string.Join(", ", buildTargets)}");

        parts.Add($"coverage: {coverageCollector}");
        parts.Add($"windows: {windowsRequirement}");

        return string.Join(" | ", parts);
    }

    private static bool IsTrue(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
