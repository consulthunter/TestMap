namespace TestMap.Persistence.Ef.Entities.FlakyTestDetection;

public class TestExecutionResultEntity
{
    public int Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string SolutionPath { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public int? TestMemberId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;
    public string ExecutionContext { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
