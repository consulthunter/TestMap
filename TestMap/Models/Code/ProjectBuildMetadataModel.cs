using TestMap.Models.Rules;

namespace TestMap.Models.Code;

public class ProjectBuildMetadataModel
{
    public List<string> BuildTargets { get; set; } = new();
    public string DefaultBuildTarget { get; set; } = string.Empty;
    public string GlobalJsonPath { get; set; } = string.Empty;
    public string SdkVersion { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public List<string> RuntimeIdentifiers { get; set; } = new();
    public string TargetPlatformIdentifier { get; set; } = string.Empty;
    public bool IsTestProject { get; set; }
    public bool UsesWindowsDesktop { get; set; }
    public WindowsRequirementType WindowsRequirement { get; set; } = WindowsRequirementType.Unknown;
    public CoverageCollectorType CoverageCollector { get; set; } = CoverageCollectorType.Unknown;
    public List<RuleDecisionRecord> RuleDecisions { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}
