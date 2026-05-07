using TestMap.Models.Results;

namespace TestMap.Persistence.Ef.Entities.Testing;

public class TestRunEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string RunDate { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int Coverage { get; set; }
    public double? MutationScore { get; set; }
    public string LogPath { get; set; } = string.Empty;
    public FailureAnalysisModel? FailureAnalysis { get; set; }
    public DateTime? CreatedAt { get; set; }
}