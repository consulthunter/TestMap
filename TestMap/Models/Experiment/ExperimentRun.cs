namespace TestMap.Models.Experiment;

/// <summary>
/// Represents a complete experiment run across multiple providers and strategies.
/// </summary>
public class ExperimentRun
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProjectId { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string CandidateSelectionStrategy { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty;
    public string ResultsFilePath { get; set; } = string.Empty;
    public int CandidateLimit { get; set; }
    public string Status { get; set; } = string.Empty;

    public virtual ICollection<CandidateMethod> CandidateMethods { get; set; } = new List<CandidateMethod>();
}
