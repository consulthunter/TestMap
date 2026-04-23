namespace TestMap.Models.Testing;

public class TestRunModel
{
    public string RunId { get; set; } = "";
    public string RunDate { get; set; } = "";
    public bool Success { get; set; }
    public int Coverage { get; set; }
    public double? MutationScore { get; set; }
    public string LogPath { get; set; } = "";
    public List<TestResultModel> Results { get; set; } = new();
    public FailureAnalysisModel? FailureAnalysis { get; set; }
}
