namespace TestMap.Models.Configuration.Testing.FlakyDetection;

public class FlakyTestDetectionConfig
{
    public bool Enabled { get; set; }
    public int MinimumExecutions { get; set; } = 3;
    public int HistoryWindowRuns { get; set; } = 20;
    public bool RerunFailedTests { get; set; } = true;
    public int RerunCount { get; set; } = 2;
    public double FlakyScoreThreshold { get; set; } = 60.0;
    public FlakinessWeightsConfig Weights { get; set; } = new();
}