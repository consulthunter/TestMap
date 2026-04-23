using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class ExperimentRunEntity
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int ProjectId { get; set; }
    [MaxLength(4000)]
    public string Configuration { get; set; } = string.Empty;
    public int CandidateLimit { get; set; }
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;
    
    public virtual ICollection<CandidateMethodEntity> CandidateMethods { get; set; } = new List<CandidateMethodEntity>();
}
