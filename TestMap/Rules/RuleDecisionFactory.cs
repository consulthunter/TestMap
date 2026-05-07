using TestMap.Models.Rules;

namespace TestMap.Rules;

public static class RuleDecisionFactory
{
    public static RuleDecisionRecord CreateDecision(
        string decisionKind,
        string value,
        RuleDefinition rule,
        RuleConfidence confidence,
        IEnumerable<RuleEvidenceRecord>? evidence = null,
        string notes = "")
    {
        return new RuleDecisionRecord
        {
            DecisionKind = decisionKind,
            Value = value,
            RuleId = rule.Id,
            RuleVersion = rule.Version,
            Confidence = confidence,
            Evidence = evidence?.ToList() ?? [],
            Notes = notes
        };
    }

    public static RuleEvidenceRecord CreateEvidence(
        string source,
        string key,
        string value,
        string location = "")
    {
        return new RuleEvidenceRecord
        {
            Source = source,
            Key = key,
            Value = value,
            Location = location
        };
    }
}
