using TestMap.Models.Rules;

namespace TestMap.Rules.TestExecution;

public static class TestExecutionRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "TestExecution";

    public static RuleDefinition DockerContextWindowsRequired { get; } = Define(
        "test-execution.docker-context.windows-required",
        "Docker context from Windows requirement",
        "A Windows-required solution uses the Windows Docker context.");

    public static RuleDefinition DockerContextDefaultLinux { get; } = Define(
        "test-execution.docker-context.default-linux",
        "Docker context defaults to Linux",
        "Blank or stale Windows Docker context defaults to the Linux Docker context when Windows is not required.");

    public static RuleDefinition DockerContextConfigured { get; } = Define(
        "test-execution.docker-context.configured",
        "Docker context from configuration",
        "A non-Windows configured Docker context is preserved when Windows is not required.");

    public static RuleDefinition WindowsRequiredByProject { get; } = Define(
        "test-execution.windows-requirement.project-required",
        "Windows required by project metadata",
        "At least one project is marked Required or LikelyRequired for Windows.");

    public static RuleDefinition WindowsNotRequiredByProjects { get; } = Define(
        "test-execution.windows-requirement.projects-not-required",
        "Windows not required by project metadata",
        "No project is marked Required or LikelyRequired for Windows.");

    public static RuleDefinition BaselineFrameworkSharedTarget { get; } = Define(
        "test-execution.baseline-framework.shared-target",
        "Baseline framework from shared target",
        "Baseline tests use the highest-preference target framework shared by all test projects.");

    public static RuleDefinition BaselineFrameworkNoSharedTarget { get; } = Define(
        "test-execution.baseline-framework.no-shared-target",
        "No shared baseline framework",
        "Baseline tests omit --framework because test projects do not share a target framework.");

    public static RuleDefinition BaselineFrameworkNoTestProjects { get; } = Define(
        "test-execution.baseline-framework.no-test-projects",
        "No baseline test projects",
        "Baseline tests omit --framework because no test projects were discovered.");

    public static RuleDefinition TargetFrameworkPreferred { get; } = Define(
        "test-execution.target-framework.preferred",
        "Preferred target framework",
        "Targeted tests use the highest-preference target framework for the selected project.");

    public static RuleDefinition TargetFrameworkMissing { get; } = Define(
        "test-execution.target-framework.missing",
        "No target framework",
        "Targeted tests omit --framework because the project has no target framework metadata.");

    public static RuleDefinition CoverageCollectorCoverlet { get; } = Define(
        "test-execution.coverage-collector.coverlet",
        "Coverlet collector argument",
        "Coverlet metadata maps to the XPlat Code Coverage collector argument.");

    public static RuleDefinition CoverageCollectorMicrosoft { get; } = Define(
        "test-execution.coverage-collector.microsoft",
        "Microsoft collector argument",
        "Microsoft or unknown coverage metadata maps to the Microsoft Code Coverage collector argument.");

    public static RuleDefinition FailureDockerEngine { get; } = Define(
        "test-execution.failure.docker-engine",
        "Docker engine failure",
        "Docker failed before the build or test workflow could start.");

    public static RuleDefinition FailurePackageRestore { get; } = Define(
        "test-execution.failure.package-restore",
        "Package restore failure",
        "NuGet package restore failed.");

    public static RuleDefinition FailureImageDependencyMissing { get; } = Define(
        "test-execution.failure.image-dependency-missing",
        "Image dependency missing",
        "The container image is missing a required build dependency.");

    public static RuleDefinition FailurePlatformMismatch { get; } = Define(
        "test-execution.failure.platform-mismatch",
        "Platform mismatch",
        "The project requires platform targeting or workloads unavailable in the selected container.");

    public static RuleDefinition FailureSdkOrWorkloadMissing { get; } = Define(
        "test-execution.failure.sdk-or-workload-missing",
        "SDK or workload missing",
        "The required .NET SDK, workload, or imported build targets are missing.");

    public static RuleDefinition FailureCompilation { get; } = Define(
        "test-execution.failure.compilation",
        "Compilation failure",
        "The project restored, but compilation failed.");

    public static RuleDefinition FailureTestExecution { get; } = Define(
        "test-execution.failure.test-execution",
        "Test execution failure",
        "The build completed, but test execution failed before producing usable results.");

    public static RuleDefinition FailureCoverageCollection { get; } = Define(
        "test-execution.failure.coverage-collection",
        "Coverage collection failure",
        "Coverage collection failed inside the container.");

    public static RuleDefinition FailureUnknown { get; } = Define(
        "test-execution.failure.unknown",
        "Unknown failure",
        "The container run failed, but no known failure pattern matched.");

    public static RuleDefinition ContainerPathLinux { get; } = Define(
        "test-execution.container-path.linux",
        "Linux container path",
        "A host path inside the project mount maps to a Linux container path.");

    public static RuleDefinition ContainerPathWindows { get; } = Define(
        "test-execution.container-path.windows",
        "Windows container path",
        "A host path inside the project mount maps to a Windows container path.");

    public static RuleDefinition ContainerPathOutsideProject { get; } = Define(
        "test-execution.container-path.outside-project",
        "Path outside project",
        "A host path outside the project mount is rejected.");

    public static RuleDefinition MountArgumentLinux { get; } = Define(
        "test-execution.mount-argument.linux",
        "Linux mount argument",
        "The project directory maps to the Linux container project root.");

    public static RuleDefinition MountArgumentWindows { get; } = Define(
        "test-execution.mount-argument.windows",
        "Windows mount argument",
        "The project directory maps to the Windows container project root.");

    public static RuleDefinition ExpectedOsLinux { get; } = Define(
        "test-execution.expected-os.linux",
        "Linux expected OS",
        "A non-Windows Docker context expects a Linux daemon.");

    public static RuleDefinition ExpectedOsWindows { get; } = Define(
        "test-execution.expected-os.windows",
        "Windows expected OS",
        "A Windows Docker context expects a Windows daemon.");

    public static RuleDefinition CleanupPreserveArtifacts { get; } = Define(
        "test-execution.cleanup.preserve-artifacts",
        "Preserve artifacts",
        "Coverage, mutation, and TestResults artifacts are preserved for diagnostics.");

    public static RuleDefinition CleanupDeleteArtifacts { get; } = Define(
        "test-execution.cleanup.delete-artifacts",
        "Delete artifacts",
        "Generated coverage, mutation, and TestResults artifacts are deleted after the run.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        DockerContextWindowsRequired,
        DockerContextDefaultLinux,
        DockerContextConfigured,
        WindowsRequiredByProject,
        WindowsNotRequiredByProjects,
        BaselineFrameworkSharedTarget,
        BaselineFrameworkNoSharedTarget,
        BaselineFrameworkNoTestProjects,
        TargetFrameworkPreferred,
        TargetFrameworkMissing,
        CoverageCollectorCoverlet,
        CoverageCollectorMicrosoft,
        FailureDockerEngine,
        FailurePackageRestore,
        FailureImageDependencyMissing,
        FailurePlatformMismatch,
        FailureSdkOrWorkloadMissing,
        FailureCompilation,
        FailureTestExecution,
        FailureCoverageCollection,
        FailureUnknown,
        ContainerPathLinux,
        ContainerPathWindows,
        ContainerPathOutsideProject,
        MountArgumentLinux,
        MountArgumentWindows,
        ExpectedOsLinux,
        ExpectedOsWindows,
        CleanupPreserveArtifacts,
        CleanupDeleteArtifacts
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
