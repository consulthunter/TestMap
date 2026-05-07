using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.TestGeneration;

namespace TestMap.Services.Experiment.Execution;

public static class ExperimentConfigurationValidator
{
    public static void ValidateMatrixSettings(ExperimentConfig config)
    {
        GenerationObjectivePolicy.Validate(
            config.Objective,
            config.Approaches ?? [],
            config.Executor);

        if (config.Approaches == null || config.Approaches.Count == 0)
            throw new InvalidOperationException("ExperimentConfig.Approaches must contain at least one approach.");

        if (config.BudgetModes == null || config.BudgetModes.Count == 0)
            throw new InvalidOperationException("ExperimentConfig.BudgetModes must contain at least one budget mode.");

        if (!config.Approaches.Contains(config.GenerationApproach))
            throw new InvalidOperationException(
                "ExperimentConfig.GenerationApproach is shadowed by ExperimentConfig.Approaches. Include it in Approaches or remove the shadowed setting.");

        if (config.ContextModes == null || config.ContextModes.Count == 0)
            throw new InvalidOperationException("ExperimentConfig.ContextModes must contain at least one context mode.");

        if (config.Approaches.Any(x => x == TestGenerationApproach.MetricsDriven) &&
            (config.MetricsPaths == null || config.MetricsPaths.Count == 0))
            throw new InvalidOperationException(
                "ExperimentConfig.MetricsPaths must contain at least one path when MetricsDriven is in Approaches.");

        if (config.Resume.Enabled && config.Resume.RewriteResultsFileOnResume)
            throw new InvalidOperationException(
                "Experiment resume uses append-only results. Set ExperimentConfig.Resume.RewriteResultsFileOnResume to false.");
    }

    public static void ValidateGenerationConfig(
        TestGenerationObjective objective,
        TestGenerationApproach approach,
        TestActionExecutorMode configuredExecutor)
    {
        GenerationObjectivePolicy.Validate(objective, [approach], configuredExecutor);
    }
}
