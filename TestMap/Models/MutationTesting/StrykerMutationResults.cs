namespace TestMap.Models.MutationTesting;

public class StrykerMutationResults
{
    public Dictionary<string, StrykerFileResult> files { get; set; } = new();
    public Dictionary<string, StrykerTestFileResult> testFiles { get; set; } = new();
    public string projectRoot { get; set; } = string.Empty;
    public string schemaVersion { get; set; } = string.Empty;
    public StrykerThresholds thresholds { get; set; } = new();
}