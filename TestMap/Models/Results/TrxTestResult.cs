namespace TestMap.Models.Results;

public class TrxTestResult
{
    public string RunId { get; set; } = "";
    public string RunDate { get; set; } = "";
    public int MethodId { get; set; } = 0;
    public string TestName { get; set; } = "";
    public string Outcome { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}