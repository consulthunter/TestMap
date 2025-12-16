namespace TestMap.Models.Results;

public class StrykerMutationResults
{
    public Dictionary<string, StrykerFileResult> files { get; set; }
    public Dictionary<string, StrykerTestFileResult> testFiles { get; set; }
    public string projectRoot { get; set; }
    public string schemaVersion { get; set; }
    public StrykerThresholds thresholds { get; set; }
}