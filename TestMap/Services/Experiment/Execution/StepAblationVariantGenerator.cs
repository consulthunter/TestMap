using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Rules.Generation;

namespace TestMap.Services.Experiment.Execution;

public sealed class StepAblationVariantGenerator : IStepAblationVariantGenerator
{
    public StepAblationVariantGenerationResult Generate(StepAblationConfig config, GenerationStepConfig baselineSteps)
    {
        var decisions = new List<RuleDecisionRecord>();

        if (!config.Enabled)
        {
            decisions.Add(Decision(
                GenerationExperimentRuleDefinitions.StepAblationDisabled,
                "StepAblationDisabled",
                "Step ablation is disabled; only the baseline variant is used."));

            return new StepAblationVariantGenerationResult
            {
                Variants = [Clone(baselineSteps, "baseline")],
                RuleDecisions = decisions
            };
        }

        var variants = new List<GenerationStepConfig>();
        var ablatedSteps = config.Steps.Distinct().ToList();

        if (config.IncludeBaseline)
        {
            variants.Add(Clone(baselineSteps, "baseline"));
            decisions.Add(Decision(
                GenerationExperimentRuleDefinitions.StepAblationBaselineIncluded,
                "StepAblationBaselineIncluded",
                "The configured baseline step variant was included."));
        }

        var combinationCount = 1 << ablatedSteps.Count;
        for (var mask = 1; mask < combinationCount; mask++)
        {
            var disabledSteps = ablatedSteps
                .Where((_, index) => (mask & (1 << index)) != 0)
                .ToList();

            if (!config.IncludeAllDisabled && disabledSteps.Count == ablatedSteps.Count)
            {
                decisions.Add(Decision(
                    GenerationExperimentRuleDefinitions.StepAblationAllDisabledExcluded,
                    "StepAblationAllDisabledExcluded",
                    "The all-disabled ablation variant was excluded by configuration."));
                continue;
            }

            variants.Add(CreateVariant(baselineSteps, disabledSteps));
        }

        var maxVariants = Math.Max(1, config.MaxVariants);
        if (variants.Count > maxVariants)
        {
            variants = variants.Take(maxVariants).ToList();
            decisions.Add(Decision(
                GenerationExperimentRuleDefinitions.StepAblationCapped,
                "StepAblationCapped",
                $"Step ablation variants were capped at {maxVariants}.",
                RuleDecisionFactory.CreateEvidence("StepAblation", "MaxVariants", maxVariants.ToString())));
        }

        decisions.Add(Decision(
            GenerationExperimentRuleDefinitions.StepAblationVariantCount,
            "StepAblationVariantCount",
            $"Generated {variants.Count} step ablation variant(s).",
            RuleDecisionFactory.CreateEvidence("StepAblation", "VariantCount", variants.Count.ToString())));

        return new StepAblationVariantGenerationResult
        {
            Variants = variants,
            RuleDecisions = decisions
        };
    }

    private static GenerationStepConfig CreateVariant(
        GenerationStepConfig baselineSteps,
        IReadOnlyList<GenerationStepType> disabledSteps)
    {
        var variant = Clone(
            baselineSteps,
            "ablated-" + string.Join("-", disabledSteps.Select(x => x.ToString())));

        foreach (var step in disabledSteps)
        {
            switch (step)
            {
                case GenerationStepType.EvidencePackage:
                    variant.EnableEvidencePackage = false;
                    break;
                case GenerationStepType.ContextGraph:
                    variant.EnableContextGraph = false;
                    break;
                case GenerationStepType.ContextResolution:
                    variant.EnableContextResolution = false;
                    break;
                case GenerationStepType.RoslynValidation:
                    variant.EnableRoslynValidation = false;
                    break;
                case GenerationStepType.Scenario:
                    variant.EnableScenario = false;
                    break;
                case GenerationStepType.MethodName:
                    variant.EnableMethodName = false;
                    break;
                case GenerationStepType.ArrangePlan:
                    variant.EnableArrangePlan = false;
                    break;
                case GenerationStepType.InputPlan:
                    variant.EnableInputPlan = false;
                    break;
                case GenerationStepType.ActionPlan:
                    variant.EnableActionPlan = false;
                    break;
                case GenerationStepType.AssertionPlan:
                    variant.EnableAssertionPlan = false;
                    break;
                case GenerationStepType.FinalTest:
                    variant.EnableFinalTest = false;
                    break;
            }
        }

        return variant;
    }

    private static GenerationStepConfig Clone(GenerationStepConfig steps, string variantId)
    {
        return new GenerationStepConfig
        {
            VariantId = variantId,
            EnableEvidencePackage = steps.EnableEvidencePackage,
            EnableContextGraph = steps.EnableContextGraph,
            EnableContextResolution = steps.EnableContextResolution,
            EnableRoslynValidation = steps.EnableRoslynValidation,
            EnableScenario = steps.EnableScenario,
            EnableMethodName = steps.EnableMethodName,
            EnableArrangePlan = steps.EnableArrangePlan,
            EnableInputPlan = steps.EnableInputPlan,
            EnableActionPlan = steps.EnableActionPlan,
            EnableAssertionPlan = steps.EnableAssertionPlan,
            EnableFinalTest = steps.EnableFinalTest
        };
    }

    private static RuleDecisionRecord Decision(
        RuleDefinition rule,
        string value,
        string notes,
        params RuleEvidenceRecord[] evidence)
    {
        return RuleDecisionFactory.CreateDecision(
            "GenerationExperiment",
            value,
            rule,
            RuleConfidence.High,
            evidence,
            notes);
    }
}
