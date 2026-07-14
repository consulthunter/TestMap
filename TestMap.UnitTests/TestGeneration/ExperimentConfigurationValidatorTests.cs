using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class ExperimentConfigurationValidatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateMatrixSettings_RejectsEmptyApproaches()
    {
        var config = ValidExperimentConfig();
        config.Approaches = [];

        var ex = Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateMatrixSettings(config));

        Assert.Contains("Approaches", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateMatrixSettings_RejectsResumeRewrite()
    {
        var config = ValidExperimentConfig();
        config.Resume.RewriteResultsFileOnResume = true;

        var ex = Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateMatrixSettings(config));

        Assert.Contains("append-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateGenerationConfig_RejectsActionAwareForTestSuiteExpansion()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateGenerationConfig(
                TestGenerationObjective.TestSuiteExpansion,
                TestGenerationApproach.ActionAware,
                TestActionExecutorMode.BasicExtension));

        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateGenerationConfig_RejectsConfiguredActionAwareExecutorForTestSuiteExpansion()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateGenerationConfig(
                TestGenerationObjective.TestSuiteExpansion,
                TestGenerationApproach.MetricsDriven,
                TestActionExecutorMode.ActionAware));

        Assert.Contains("requires executor", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateGenerationConfig_AllowsMetricsDrivenBasicExecutorForTestSuiteExpansion()
    {
        ExperimentConfigurationValidator.ValidateGenerationConfig(
            TestGenerationObjective.TestSuiteExpansion,
            TestGenerationApproach.MetricsDriven,
            TestActionExecutorMode.BasicExtension);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateMatrixSettings_CohortModeRequiresId()
    {
        var config = ValidExperimentConfig();
        config.CandidateCohort.Mode = CandidateCohortMode.Create;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateMatrixSettings(config));

        Assert.Contains("CandidateCohort.Id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateMatrixSettings_ReuseRejectsSeedOverride()
    {
        var config = ValidExperimentConfig();
        config.CandidateCohort = new CandidateCohortConfig
        {
            Id = "cohort-a",
            Mode = CandidateCohortMode.Reuse,
            RandomSeed = 42
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ExperimentConfigurationValidator.ValidateMatrixSettings(config));

        Assert.Contains("must be omitted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ExperimentConfig ValidExperimentConfig()
    {
        return new ExperimentConfig
        {
            Approaches = [TestGenerationApproach.MetricsDriven],
            MetricsPaths = [MetricsDrivenPath.CoverageAndMutation],
            BudgetModes = [GenerationBudgetMode.PassAt1],
            ContextModes = [GenerationContextMode.ChainedHistory]
        };
    }
}
