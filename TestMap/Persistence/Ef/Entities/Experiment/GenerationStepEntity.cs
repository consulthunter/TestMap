using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class GenerationStepEntity
{
    public int Id { get; set; }
    public int GenerationAttemptId { get; set; }
    [MaxLength(50)]
    public string StepName { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ValidationResult { get; set; } = string.Empty;
    
    public virtual GenerationAttemptEntity? GenerationAttempt { get; set; }
}
