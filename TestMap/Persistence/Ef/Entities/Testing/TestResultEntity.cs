namespace TestMap.Persistence.Ef.Entities.Testing;

public class TestResultEntity
{
    public int Id { get; set; }
    public int TestRunId { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string RunDate { get; set; } = string.Empty;
    public int MethodId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
}