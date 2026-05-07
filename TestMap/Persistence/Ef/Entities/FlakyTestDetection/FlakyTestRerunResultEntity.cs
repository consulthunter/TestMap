namespace TestMap.Persistence.Ef.Entities.FlakyTestDetection;

public class FlakyTestRerunResultEntity
{
    public int Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public int TestExecutionResultId { get; set; }
    public int AttemptNumber { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}