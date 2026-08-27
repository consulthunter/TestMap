using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.StaticAnalysis.Assertions;
using TestMap.Services.TestGeneration;

namespace TestMap.Services.Experiment.Execution;

public static class ExperimentConfigurationValidator
{
    public static void ValidateMatrixSettings(ExperimentConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        GenerationObjectivePolicy.Validate(
            config.Objective,
            config.Approaches ?? [],
            GenerationObjectivePolicy.ResolveExecutor(config.Objective));

        if (config.Approaches == null || config.Approaches.Count == 0)
            throw new InvalidOperationException("ExperimentConfig.Approaches must contain at least one approach.");

        if (config.BudgetModes == null || config.BudgetModes.Count == 0)
            throw new InvalidOperationException("ExperimentConfig.BudgetModes must contain at least one budget mode.");

        if (config.ContextModes == null || config.ContextModes.Count == 0)
            throw new InvalidOperationException("ExperimentConfig.ContextModes must contain at least one context mode.");

        if (config.Approaches.Any(x => x == TestGenerationApproach.MetricsDriven) &&
            (config.MetricsPaths == null || config.MetricsPaths.Count == 0))
            throw new InvalidOperationException(
                "ExperimentConfig.MetricsPaths must contain at least one path when MetricsDriven is in Approaches.");

        if (config.Resume.Enabled && config.Resume.RewriteResultsFileOnResume)
            throw new InvalidOperationException(
                "Experiment resume uses append-only results. Set ExperimentConfig.Resume.RewriteResultsFileOnResume to false.");

        if (config.CandidateCohort.Mode != CandidateCohortMode.Disabled &&
            string.IsNullOrWhiteSpace(config.CandidateCohort.Id))
            throw new InvalidOperationException(
                "ExperimentConfig.CandidateCohort.Id is required when candidate cohort mode is create or reuse.");

        if (config.CandidateCohort.Mode == CandidateCohortMode.Reuse &&
            config.CandidateCohort.RandomSeed.HasValue)
            throw new InvalidOperationException(
                "ExperimentConfig.CandidateCohort.RandomSeed is defined by the stored cohort and must be omitted in reuse mode.");

        try
        {
            AssertionLineagePolicy.Validate(config.Evaluation.Assertions);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException(
                "ExperimentConfig.Evaluation.Assertions contains an invalid assertion-lineage policy.",
                exception);
        }
    }

    public static void ValidateGenerationConfig(
        TestGenerationObjective objective,
        TestGenerationApproach approach,
        TestActionExecutorMode configuredExecutor)
    {
        GenerationObjectivePolicy.Validate(objective, [approach], configuredExecutor);
    }
}
