using TestMap.Models.Rules;

namespace TestMap.Rules.ProjectDiscovery;

public static class ProjectDiscoveryRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "ProjectDiscovery";

    public static RuleDefinition ProjectFileParsed { get; } = Define(
        "project-discovery.project-file.parsed",
        "Project file parsed",
        "Project discovery can read the project XML.");

    public static RuleDefinition ProjectFileMissing { get; } = Define(
        "project-discovery.project-file.missing",
        "Project file missing",
        "Project discovery could not find a project file to inspect.");

    public static RuleDefinition ProjectFileParseFailed { get; } = Define(
        "project-discovery.project-file.parse-failed",
        "Project file parse failed",
        "Project discovery found a project file, but XML parsing failed.");

    public static RuleDefinition BuildTargetsFromTargetFramework { get; } = Define(
        "project-discovery.build-targets.target-framework",
        "Build targets from TargetFramework",
        "A single TargetFramework property defines the build target.");

    public static RuleDefinition BuildTargetsFromTargetFrameworks { get; } = Define(
        "project-discovery.build-targets.target-frameworks",
        "Build targets from TargetFrameworks",
        "A semicolon-delimited TargetFrameworks property defines the build targets.");

    public static RuleDefinition BuildTargetsMissing { get; } = Define(
        "project-discovery.build-targets.missing",
        "Build targets missing",
        "No TargetFramework or TargetFrameworks value was available.");

    public static RuleDefinition BuildTargetsUnavailable { get; } = Define(
        "project-discovery.build-targets.unavailable",
        "Build targets unavailable",
        "Build targets cannot be read because project XML was unavailable.");

    public static RuleDefinition SdkVersionFromGlobalJson { get; } = Define(
        "project-discovery.sdk-version.global-json",
        "SDK version from global.json",
        "The nearest global.json supplied an SDK version.");

    public static RuleDefinition SdkVersionMissingGlobalJson { get; } = Define(
        "project-discovery.sdk-version.no-global-json",
        "No global.json",
        "No nearest global.json was found for the project.");

    public static RuleDefinition SdkVersionUnreadableGlobalJson { get; } = Define(
        "project-discovery.sdk-version.unreadable-global-json",
        "Unreadable global.json",
        "A nearest global.json was found, but no SDK version could be read.");

    public static RuleDefinition TestProjectExplicit { get; } = Define(
        "project-discovery.test-project.explicit",
        "Explicit test project",
        "The project explicitly sets IsTestProject=true.");

    public static RuleDefinition TestProjectByName { get; } = Define(
        "project-discovery.test-project.name",
        "Test project by name",
        "The Roslyn project name follows the .Tests naming convention.");

    public static RuleDefinition TestProjectByPackage { get; } = Define(
        "project-discovery.test-project.package",
        "Test project by package",
        "The project references a known .NET test package.");

    public static RuleDefinition TestProjectNoSignal { get; } = Define(
        "project-discovery.test-project.no-signal",
        "No test project signal",
        "No explicit, naming, or package signal identified the project as a test project.");

    public static RuleDefinition WindowsRequiredByDesktop { get; } = Define(
        "project-discovery.windows-requirement.desktop",
        "Windows required by desktop UI",
        "UseWPF or UseWindowsForms indicates a Windows desktop workload.");

    public static RuleDefinition WindowsRequiredByTargetPlatform { get; } = Define(
        "project-discovery.windows-requirement.target-platform",
        "Windows required by target platform",
        "TargetPlatformIdentifier is Windows.");

    public static RuleDefinition WindowsRequiredByWindowsTargetFramework { get; } = Define(
        "project-discovery.windows-requirement.target-framework-windows",
        "Windows required by target framework",
        "A target framework includes a -windows platform suffix.");

    public static RuleDefinition WindowsLikelyByRuntimeIdentifier { get; } = Define(
        "project-discovery.windows-requirement.runtime-identifier",
        "Windows likely by runtime identifier",
        "RuntimeIdentifier starts with win.");

    public static RuleDefinition WindowsLikelyByRuntimeIdentifiers { get; } = Define(
        "project-discovery.windows-requirement.runtime-identifiers",
        "Windows likely by runtime identifiers",
        "RuntimeIdentifiers contains a value that starts with win.");

    public static RuleDefinition WindowsLikelyByPackage { get; } = Define(
        "project-discovery.windows-requirement.package",
        "Windows likely by package",
        "A package reference is Windows-oriented.");

    public static RuleDefinition WindowsNotRequiredNoSignal { get; } = Define(
        "project-discovery.windows-requirement.no-signal",
        "Windows not required without signal",
        "Build targets were discovered and no Windows-specific project signal was found.");

    public static RuleDefinition WindowsUnknownNoBuildTargets { get; } = Define(
        "project-discovery.windows-requirement.no-build-targets",
        "Windows unknown without build targets",
        "No build targets were discovered, so Windows requirement cannot be determined.");

    public static RuleDefinition CoverageCollectorCoverlet { get; } = Define(
        "project-discovery.coverage-collector.coverlet",
        "Coverlet collector",
        "The project references coverlet.collector.");

    public static RuleDefinition CoverageCollectorMicrosoft { get; } = Define(
        "project-discovery.coverage-collector.microsoft",
        "Microsoft coverage collector",
        "The project references a Microsoft code coverage package.");

    public static RuleDefinition CoverageCollectorUnknown { get; } = Define(
        "project-discovery.coverage-collector.unknown",
        "Unknown coverage collector",
        "No known coverage collector package was found.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        ProjectFileParsed,
        ProjectFileMissing,
        ProjectFileParseFailed,
        BuildTargetsFromTargetFramework,
        BuildTargetsFromTargetFrameworks,
        BuildTargetsMissing,
        BuildTargetsUnavailable,
        SdkVersionFromGlobalJson,
        SdkVersionMissingGlobalJson,
        SdkVersionUnreadableGlobalJson,
        TestProjectExplicit,
        TestProjectByName,
        TestProjectByPackage,
        TestProjectNoSignal,
        WindowsRequiredByDesktop,
        WindowsRequiredByTargetPlatform,
        WindowsRequiredByWindowsTargetFramework,
        WindowsLikelyByRuntimeIdentifier,
        WindowsLikelyByRuntimeIdentifiers,
        WindowsLikelyByPackage,
        WindowsNotRequiredNoSignal,
        WindowsUnknownNoBuildTargets,
        CoverageCollectorCoverlet,
        CoverageCollectorMicrosoft,
        CoverageCollectorUnknown
    ];

    private static RuleDefinition Define(string id, string name, string description)
    {
        return new RuleDefinition
        {
            Id = id,
            Version = Version,
            Name = name,
            Description = description,
            Category = Category
        };
    }
}
