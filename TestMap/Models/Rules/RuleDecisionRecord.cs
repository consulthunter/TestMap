namespace TestMap.Models.Rules;

public class RuleDecisionRecord
{
    public string DecisionKind { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleVersion { get; set; } = string.Empty;
    public RuleConfidence Confidence { get; set; } = RuleConfidence.Unknown;
    public List<RuleEvidenceRecord> Evidence { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}
