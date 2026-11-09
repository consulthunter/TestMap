namespace TestMap.Models.Testing;

public class GeneratedTestRunModel : TestRunModel
{
    public string CoveredMethod { get; set; } = "";
    public double? MethodCoverage { get; set; }
    public string MethodCoverageStatus { get; set; } = "NotRequested";
    public string MethodCoverageReason { get; set; } = string.Empty;
}
