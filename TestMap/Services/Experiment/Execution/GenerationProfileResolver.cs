using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration;

namespace TestMap.Services.Experiment.Execution;

public static class GenerationProfileResolver
{
    public static GenerationProfile ResolveBaseProfile(GenerationConfig config, string modelName = "")
    {
        return new GenerationProfile
        {
            Objective = config.Objective,
            Provider = config.Provider,
            ModelName = modelName,
            Approach = config.Strategy,
            MetricsPath = config.Strategy == TestGenerationApproach.Naive ? null : config.MetricsPath,
            Executor = GenerationObjectivePolicy.ResolveExecutor(config.Objective),
            BudgetMode = config.BudgetMode,
            ContextMode = config.ContextMode,
            Steps = Clone(config.Steps, config.Steps.VariantId),
            Temperature = config.Temperature,
            StepErrorRetries = Math.Max(0, config.StepErrorRetries),
            StepRetryDelayMs = Math.Max(0, config.StepRetryDelayMs)
        };
    }

    public static GenerationProfile ResolveEffectiveProfile(
        GenerationConfig generationConfig,
        ExperimentConfig experimentConfig,
        GenerationExperimentMatrixItem matrixItem)
    {
        var profile = ResolveBaseProfile(generationConfig, matrixItem.ModelName);

        profile.Objective = experimentConfig.Objective;
        profile.Provider = matrixItem.Provider;
        profile.ModelName = matrixItem.ModelName;
        profile.Approach = matrixItem.Approach;
        profile.MetricsPath = matrixItem.MetricsPath;
        profile.Executor = GenerationObjectivePolicy.ResolveExecutor(experimentConfig.Objective);
        profile.BudgetMode = matrixItem.BudgetMode;
        profile.ContextMode = matrixItem.ContextMode;
        profile.Steps = Clone(matrixItem.Steps, matrixItem.Steps.VariantId);
        profile.Temperature = matrixItem.Temperature;
        profile.StepErrorRetries = Math.Max(0, experimentConfig.StepErrorRetries);
        profile.StepRetryDelayMs = Math.Max(0, experimentConfig.StepRetryDelayMs);

        return profile;
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
}
