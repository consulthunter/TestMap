using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.Rules.Generation;

public static class GenerationAcceptanceDecisionEngine
{
    public static IReadOnlyList<RuleDecisionRecord> Evaluate(
        GenerationValidationResult validation,
        TestAcceptanceConfig config)
    {
        var decisions = new List<RuleDecisionRecord>();

        AddRequirement(decisions, config.RequireCompilationSuccess, validation.CompilationSucceeded,
            GenerationAcceptanceRuleDefinitions.CompilationSatisfied,
            GenerationAcceptanceRuleDefinitions.CompilationFailed,
            "Compilation did not succeed.");

        AddRequirement(decisions, config.RequireTestsToRun, validation.TestsExecuted,
            GenerationAcceptanceRuleDefinitions.TestsRan,
            GenerationAcceptanceRuleDefinitions.TestsDidNotRun,
            "No tests were executed.");

        AddRequirement(decisions, config.RequireAllTestsPass, validation.AllTestsPassed,
            GenerationAcceptanceRuleDefinitions.AllTestsPassed,
            GenerationAcceptanceRuleDefinitions.TestFailures,
            "One or more tests failed.");

        AddRequirement(decisions, config.RequireCoverageImprovement, validation.CoverageImproved,
            GenerationAcceptanceRuleDefinitions.CoverageSatisfied,
            GenerationAcceptanceRuleDefinitions.CoverageMissing,
            "Coverage did not improve.");

        if (config.RequireCoverageImprovement)
        {
            var minSatisfied = validation.CoverageImprovement > config.MinCoverageImprovement;
            decisions.Add(Decision(
                minSatisfied
                    ? GenerationAcceptanceRuleDefinitions.MinCoverageSatisfied
                    : GenerationAcceptanceRuleDefinitions.MinCoverageMissing,
                minSatisfied ? "AcceptedMinCoverageDelta" : "RejectedMinCoverageDelta",
                minSatisfied
                    ? "Minimum coverage improvement was satisfied."
                    : "Coverage did not improve enough.",
                RuleDecisionFactory.CreateEvidence("Coverage", "Delta", validation.CoverageImprovement.ToString("R")),
                RuleDecisionFactory.CreateEvidence("Coverage", "MinimumDelta", config.MinCoverageImprovement.ToString("R"))));
        }

        return decisions;
    }

    private static void AddRequirement(
        List<RuleDecisionRecord> decisions,
        bool required,
        bool satisfied,
        RuleDefinition satisfiedRule,
        RuleDefinition failedRule,
        string failureReason)
    {
        if (!required) return;

        decisions.Add(Decision(
            satisfied ? satisfiedRule : failedRule,
            satisfied ? $"Accepted{satisfiedRule.Name.Replace(" ", string.Empty)}" : $"Rejected{failedRule.Name.Replace(" ", string.Empty)}",
            satisfied ? satisfiedRule.Description : failureReason));
    }

    private static RuleDecisionRecord Decision(
        RuleDefinition rule,
        string value,
        string notes,
        params RuleEvidenceRecord[] evidence)
    {
        return RuleDecisionFactory.CreateDecision(
            "GenerationAcceptance",
            value,
            rule,
            RuleConfidence.High,
            evidence,
            notes);
    }
}
