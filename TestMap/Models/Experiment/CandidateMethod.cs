namespace TestMap.Models.Experiment;

/// <summary>
/// Represents a source method selected as a candidate for test generation.
/// </summary>
public class CandidateMethod
{
    public int Id { get; set; }
    public int ExperimentRunId { get; set; }
    public int MemberId { get; set; }
    public int? ExistingTestMemberId { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? ExistingTestMethodName { get; set; }
    public double BaselineCoverage { get; set; }
    public double ComplexityScore { get; set; }
    public DateTime SelectionTime { get; set; }
    
    public virtual ExperimentRun? ExperimentRun { get; set; }
    public virtual ICollection<GenerationAttempt> GenerationAttempts { get; set; } = new List<GenerationAttempt>();
}
