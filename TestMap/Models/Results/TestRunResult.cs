namespace TestMap.Models.Results;

public class TestRunResult
{
    public string RunId { get; set; } = "";
    public string RunDate { get; set; } = "";
    public bool Success { get; set; }
    public int Coverage { get; set; }
    public string CoveredMethod { get; set; }
    public double MethodCoverage { get; set; }
    public string LogPath { get; set; } = "";
    public List<TrxTestResult> Results { get; set; } = new();
}
