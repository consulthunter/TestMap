using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Rules;

namespace TestMap.Rules.Generation;

public static class GenerationEvidenceDecisionEngine
{
    public static RuleDecisionRecord IncludeProjectContext(int memberId)
    {
        return RuleDecisionFactory.CreateDecision(
            "GenerationEvidence",
            "ProjectContextIncluded",
            GenerationEvidenceRuleDefinitions.ProjectContextIncluded,
            RuleConfidence.High,
            [RuleDecisionFactory.CreateEvidence("CandidateMethod", "MemberId", memberId.ToString())],
            "Basic target method and test project context are included for generation.");
    }

    public static RuleDecisionRecord SuppressMetricsForNaiveApproach(TestGenerationApproach approach)
    {
        return RuleDecisionFactory.CreateDecision(
            "GenerationEvidence",
            "MetricEvidenceSuppressed",
            GenerationEvidenceRuleDefinitions.NaiveSuppressesMetricEvidence,
            RuleConfidence.High,
            [RuleDecisionFactory.CreateEvidence("GenerationApproach", "Approach", approach.ToString())],
            "Naive generation keeps the same candidate but omits explicit metric evidence.");
    }

    public static RuleDecisionRecord IncludeCoverageEvidence(int gapCount, MetricsDrivenPath? metricsPath)
    {
        return RuleDecisionFactory.CreateDecision(
            "GenerationEvidence",
            "CoverageEvidenceIncluded",
            GenerationEvidenceRuleDefinitions.CoverageEvidenceIncluded,
            RuleConfidence.High,
            [
                RuleDecisionFactory.CreateEvidence("CoverageGaps", "Count", gapCount.ToString()),
                RuleDecisionFactory.CreateEvidence("GenerationApproach", "MetricsPath", metricsPath?.ToString() ?? string.Empty)
            ],
            "Coverage gap evidence is included for metrics-driven generation.");
    }

    public static RuleDecisionRecord CoverageEvidenceUnavailable(MetricsDrivenPath? metricsPath)
    {
        return RuleDecisionFactory.CreateDecision(
            "GenerationEvidence",
            "CoverageEvidenceUnavailable",
            GenerationEvidenceRuleDefinitions.CoverageEvidenceUnavailable,
            RuleConfidence.Medium,
            [RuleDecisionFactory.CreateEvidence("GenerationApproach", "MetricsPath", metricsPath?.ToString() ?? string.Empty)],
            "Coverage evidence was requested, but no coverage gaps were available.");
    }

    public static RuleDecisionRecord IncludeMutationEvidence(int mutantCount, MetricsDrivenPath? metricsPath)
    {
        return RuleDecisionFactory.CreateDecision(
            "GenerationEvidence",
            "MutationEvidenceIncluded",
            GenerationEvidenceRuleDefinitions.MutationEvidenceIncluded,
            RuleConfidence.High,
            [
                RuleDecisionFactory.CreateEvidence("Mutants", "UndetectedCount", mutantCount.ToString()),
                RuleDecisionFactory.CreateEvidence("GenerationApproach", "MetricsPath", metricsPath?.ToString() ?? string.Empty)
            ],
            "Surviving or no-coverage mutant evidence is included for metrics-driven generation.");
    }

    public static RuleDecisionRecord MutationEvidenceUnavailable(MetricsDrivenPath? metricsPath)
    {
        return RuleDecisionFactory.CreateDecision(
            "GenerationEvidence",
            "MutationEvidenceUnavailable",
            GenerationEvidenceRuleDefinitions.MutationEvidenceUnavailable,
            RuleConfidence.Medium,
            [RuleDecisionFactory.CreateEvidence("GenerationApproach", "MetricsPath", metricsPath?.ToString() ?? string.Empty)],
            "Mutation evidence was requested, but no surviving or no-coverage mutants were available.");
    }
}
