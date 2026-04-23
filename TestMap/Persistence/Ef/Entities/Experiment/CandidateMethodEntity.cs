using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class CandidateMethodEntity
{
    public int Id { get; set; }
    public int ExperimentRunId { get; set; }
    public int SourceMemberId { get; set; }
    public int? ExistingTestMemberId { get; set; }
    [MaxLength(500)]
    public string SourceMethodName { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string SourceMethodSignature { get; set; } = string.Empty;
    [MaxLength(500)]
    public string ExistingTestMethodName { get; set; } = string.Empty;
    public double InitialCoverage { get; set; }
    public int InitialCoveredLines { get; set; }
    public int InitialTotalLines { get; set; }
    public DateTime SelectionTime { get; set; }
    
    public virtual ExperimentRunEntity? ExperimentRun { get; set; }
    public virtual ICollection<GenerationAttemptEntity> GenerationAttempts { get; set; } = new List<GenerationAttemptEntity>();
}
