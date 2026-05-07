namespace TestMap.Models.Configuration;

public sealed class ExperimentResumeConfig
{
    public bool Enabled { get; set; } = true;
    public string? ResumeRunId { get; set; }
    public int RunningAttemptTimeoutMinutes { get; set; } = 60;
    public bool RewriteResultsFileOnResume { get; set; } = false;
}
