using TestMap.Models.Rules;

namespace TestMap.Models.Results;

public class FailureAnalysisModel
{
    public string Stage { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string RemediationSuggestion { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<string> MatchedPatterns { get; set; } = new();
    public RuleDecisionRecord? RuleDecision { get; set; }
}
