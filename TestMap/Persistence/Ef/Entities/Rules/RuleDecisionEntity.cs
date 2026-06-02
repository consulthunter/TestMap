using TestMap.Models.Rules;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Entities.Rules;

public class RuleDecisionEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? CSharpProjectId { get; set; }
    public string ScopeKind { get; set; } = string.Empty;
    public string ScopeId { get; set; } = string.Empty;
    public int? ExperimentRunId { get; set; }
    public int? CandidateMethodId { get; set; }
    public int? GenerationAttemptId { get; set; }
    public int? GeneratedTestExecutionId { get; set; }
    public string DecisionKind { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleVersion { get; set; } = string.Empty;
    public RuleConfidence Confidence { get; set; } = RuleConfidence.Unknown;
    public List<RuleEvidenceRecord> Evidence { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public virtual ExperimentRunEntity? ExperimentRun { get; set; }
    public virtual CandidateMethodEntity? CandidateMethod { get; set; }
    public virtual GenerationAttemptEntity? GenerationAttempt { get; set; }
    public virtual GeneratedTestExecutionEntity? GeneratedTestExecution { get; set; }
}
