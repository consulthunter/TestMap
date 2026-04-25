using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using TestMap.App;
using TestMap.Models.Code;

namespace TestMap.Services.ProjectDiscovery;

public class ProjectBuildAnalysisService(ProjectContext context) : IProjectBuildAnalysisService
{
    private static readonly string[] TestPackageMarkers =
    [
        "microsoft.net.test.sdk",
        "xunit",
        "nunit",
        "mstest.testframework",
        "mstest"
    ];

    public Task<ProjectBuildMetadataModel> AnalyzeAsync(Microsoft.CodeAnalysis.Project project)
    {
        if (string.IsNullOrWhiteSpace(project.FilePath) || !File.Exists(project.FilePath))
            return Task.FromResult(new ProjectBuildMetadataModel
            {
                Notes = "Project file path is missing."
            });

        var packageReferences = ReadPackageReferences(project.FilePath);
        var nearestGlobalJson = FindNearestGlobalJson(project.FilePath);
        using var projectCollection = new ProjectCollection();
        Microsoft.Build.Evaluation.Project? evaluatedProject = null;

        try
        {
            evaluatedProject = projectCollection.LoadProject(project.FilePath);
        }
        catch (Exception ex)
        {
            context.Project.Logger?.Warning($"MSBuild evaluation failed for {project.FilePath}: {ex.Message}");
        }

        var targetFramework = GetEffectiveProperty(evaluatedProject, "TargetFramework");
        var targetFrameworks = SplitMultiValue(GetEffectiveProperty(evaluatedProject, "TargetFrameworks"));
        var buildTargets = !string.IsNullOrWhiteSpace(targetFramework)
            ? new List<string> { targetFramework }
            : targetFrameworks;

        var runtimeIdentifier = GetEffectiveProperty(evaluatedProject, "RuntimeIdentifier");
        var runtimeIdentifiers = SplitMultiValue(GetEffectiveProperty(evaluatedProject, "RuntimeIdentifiers"));
        var targetPlatformIdentifier = GetEffectiveProperty(evaluatedProject, "TargetPlatformIdentifier");
        var useWpf = IsTrue(GetEffectiveProperty(evaluatedProject, "UseWPF"));
        var useWindowsForms = IsTrue(GetEffectiveProperty(evaluatedProject, "UseWindowsForms"));
        var usesWindowsDesktop = useWpf || useWindowsForms;
        var isTestProject = DetectTestProject(project, packageReferences, evaluatedProject);
        var windowsRequirement = DetermineWindowsRequirement(
            buildTargets,
            runtimeIdentifier,
            runtimeIdentifiers,
            targetPlatformIdentifier,
            usesWindowsDesktop,
            packageReferences);
        var coverageCollector = DetermineCoverageCollector(packageReferences, isTestProject);
        var sdkVersion = ReadSdkVersion(nearestGlobalJson);

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
            Notes = BuildNotes(sdkVersion, nearestGlobalJson, buildTargets, coverageCollector, windowsRequirement)
        });
    }

    private static string GetEffectiveProperty(Microsoft.Build.Evaluation.Project? project, string propertyName)
    {
        return project?.GetPropertyValue(propertyName)?.Trim() ?? string.Empty;
    }

    private static List<string> ReadPackageReferences(string projectFilePath)
    {
        try
        {
            var doc = XDocument.Load(projectFilePath);
            return doc.Descendants()
                .Where(x => x.Name.LocalName == "PackageReference")
                .Select(x => x.Attribute("Include")?.Value ?? x.Attribute("Update")?.Value ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
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

    private static bool DetectTestProject(
        Microsoft.CodeAnalysis.Project project,
        List<string> packageReferences,
        Microsoft.Build.Evaluation.Project? evaluatedProject)
    {
        if (IsTrue(GetEffectiveProperty(evaluatedProject, "IsTestProject"))) return true;

        if (project.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)) return true;

        return packageReferences.Any(x => TestPackageMarkers.Any(marker =>
            x.Contains(marker, StringComparison.OrdinalIgnoreCase)));
    }

    private static WindowsRequirementType DetermineWindowsRequirement(
        List<string> buildTargets,
        string runtimeIdentifier,
        List<string> runtimeIdentifiers,
        string targetPlatformIdentifier,
        bool usesWindowsDesktop,
        List<string> packageReferences)
    {
        if (usesWindowsDesktop ||
            string.Equals(targetPlatformIdentifier, "Windows", StringComparison.OrdinalIgnoreCase) ||
            buildTargets.Any(x => x.Contains("-windows", StringComparison.OrdinalIgnoreCase)))
            return WindowsRequirementType.Required;

        if ((!string.IsNullOrWhiteSpace(runtimeIdentifier) &&
             runtimeIdentifier.StartsWith("win", StringComparison.OrdinalIgnoreCase)) ||
            runtimeIdentifiers.Any(x => x.StartsWith("win", StringComparison.OrdinalIgnoreCase)) ||
            packageReferences.Any(x =>
                x.Contains("windowsdesktop", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("microsoft.windows", StringComparison.OrdinalIgnoreCase)))
            return WindowsRequirementType.LikelyRequired;

        return WindowsRequirementType.NotRequired;
    }

    private static CoverageCollectorType DetermineCoverageCollector(List<string> packageReferences, bool isTestProject)
    {
        if (packageReferences.Any(x => x.Equals("coverlet.collector", StringComparison.OrdinalIgnoreCase)))
            return CoverageCollectorType.Coverlet;

        if (packageReferences.Any(x =>
                x.Equals("Microsoft.CodeCoverage", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("Microsoft.Testing.Extensions.CodeCoverage", StringComparison.OrdinalIgnoreCase)))
            return CoverageCollectorType.MicrosoftCodeCoverage;

        return CoverageCollectorType.Unknown;
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