using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Rules.Generation;

namespace TestMap.Services.Experiment.Execution;

public sealed class GenerationExperimentMatrixGenerator : IGenerationExperimentMatrixGenerator
{
    private readonly TestMapConfig _testMapConfig;
    private readonly IStepAblationVariantGenerator _stepAblationVariantGenerator;

    public GenerationExperimentMatrixGenerator(
        TestMapConfig testMapConfig,
        IStepAblationVariantGenerator stepAblationVariantGenerator)
    {
        _testMapConfig = testMapConfig;
        _stepAblationVariantGenerator = stepAblationVariantGenerator;
    }

    public GenerationExperimentMatrix Generate(
        ExperimentConfig config,
        IReadOnlyList<AiProvider> providers)
    {
        var decisions = new List<RuleDecisionRecord>();
        var contextModes = GetContextModes(config, decisions);
        var stepVariants = _stepAblationVariantGenerator.Generate(
            config.StepAblation,
            _testMapConfig.TestingConfig.GenerationConfig.Steps);
        decisions.AddRange(stepVariants.RuleDecisions);

        var approaches = config.Approaches.Count > 0
            ? config.Approaches.Distinct().ToList()
            : [config.GenerationApproach];
        var budgetModes = config.BudgetModes.Count > 0
            ? config.BudgetModes.Distinct().ToList()
            : [GenerationBudgetMode.PassAt1];

        var items = new List<GenerationExperimentMatrixItem>();

        foreach (var provider in providers.Distinct())
        foreach (var approach in approaches)
        foreach (var metricsPath in GetMetricsPaths(config, approach, decisions))
        foreach (var contextMode in contextModes)
        foreach (var budgetMode in budgetModes)
        foreach (var steps in stepVariants.Variants)
        {
            var modelName = ResolveModelName(provider);
            var variantId = string.Join(
                "__",
                provider,
                approach,
                metricsPath?.ToString() ?? "NoMetrics",
                contextMode,
                budgetMode,
                steps.VariantId);

            items.Add(new GenerationExperimentMatrixItem
            {
                VariantId = variantId,
                Provider = provider,
                ModelName = modelName,
                Approach = approach,
                MetricsPath = metricsPath,
                ContextMode = contextMode,
                BudgetMode = budgetMode,
                Steps = steps,
                Temperature = config.Temperature,
                EffectiveProfile = GenerationProfileResolver.ResolveEffectiveProfile(
                    _testMapConfig.TestingConfig.GenerationConfig,
                    config,
                    new GenerationExperimentMatrixItem
                    {
                        VariantId = variantId,
                        Provider = provider,
                        ModelName = modelName,
                        Approach = approach,
                        MetricsPath = metricsPath,
                        ContextMode = contextMode,
                        BudgetMode = budgetMode,
                        Steps = steps,
                        Temperature = config.Temperature
                    })
            });
        }

        return new GenerationExperimentMatrix
        {
            Items = items,
            RuleDecisions = decisions
        };
    }

    private string ResolveModelName(AiProvider provider)
    {
        return _testMapConfig.AiProviderConfig.GetProviderConfig(provider)?.Model ?? string.Empty;
    }

    private static IReadOnlyList<GenerationContextMode> GetContextModes(
        ExperimentConfig config,
        List<RuleDecisionRecord> decisions)
    {
        if (config.CompareHistoryModes)
        {
            decisions.Add(Decision(
                GenerationExperimentRuleDefinitions.HistoryComparisonExpanded,
                "HistoryComparisonExpanded",
                "History comparison expanded to no-history and chained-history variants."));

            return [GenerationContextMode.NoHistory, GenerationContextMode.ChainedHistory];
        }

        decisions.Add(Decision(
            GenerationExperimentRuleDefinitions.SingleHistoryModeUsed,
            "SingleHistoryModeUsed",
            "Configured context modes were used without expanding history comparison."));

        return config.ContextModes.Count > 0
            ? config.ContextModes.Distinct().ToList()
            : [GenerationContextMode.ChainedHistory];
    }

    private static IReadOnlyList<MetricsDrivenPath?> GetMetricsPaths(
        ExperimentConfig config,
        TestGenerationApproach approach,
        List<RuleDecisionRecord> decisions)
    {
        if (approach == TestGenerationApproach.Naive)
        {
            decisions.Add(Decision(
                GenerationExperimentRuleDefinitions.MetricsPathSkippedForNaive,
                "MetricsPathSkippedForNaive",
                "Metrics path expansion was skipped for the naive approach."));

            return [null];
        }

        if (approach == TestGenerationApproach.MetricsDriven)
        {
            decisions.Add(Decision(
                GenerationExperimentRuleDefinitions.MetricsPathExpanded,
                "MetricsPathExpanded",
                "Metrics paths were expanded for a metrics-driven approach."));

            return config.MetricsPaths.Count > 0
                ? config.MetricsPaths.Distinct().Cast<MetricsDrivenPath?>().ToList()
                : [MetricsDrivenPath.CoverageAndMutation];
        }

        return [config.MetricsPaths.FirstOrDefault()];
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
