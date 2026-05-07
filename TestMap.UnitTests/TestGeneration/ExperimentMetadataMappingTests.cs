using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;
using ExperimentTestExecution = TestMap.Models.Experiment.TestExecution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class ExperimentMetadataMappingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void GenerationAttemptMapping_RoundTripsExperimentMetadata()
    {
        var attempt = new GenerationAttempt
        {
            CandidateMethodId = 12,
            Provider = AiProvider.OpenAi,
            Objective = TestGenerationObjective.TestSuiteExpansion,
            GenerationApproach = TestGenerationApproach.MetricsDriven,
            MetricsPath = MetricsDrivenPath.Mutation,
            ContextMode = GenerationContextMode.NoHistory,
            BudgetMode = GenerationBudgetMode.PassAt1RepairAt5,
            AblationVariantId = "ablated-Scenario",
            StepConfigJson = "{\"VariantId\":\"ablated-Scenario\"}",
            EffectiveProfileJson = "{\"approach\":\"MetricsDriven\"}",
            EffectiveProfileHash = "profile-hash",
            Temperature = 0.6,
            AttemptNumber = 2,
            IsRepairAttempt = true,
            ParentAttemptId = 9,
            RuleDecisionJson = "[]",
            StartedAt = DateTime.UtcNow
        };

        var roundTrip = attempt.ToEntity().ToDomain();

        Assert.Equal(TestGenerationApproach.MetricsDriven, roundTrip.GenerationApproach);
        Assert.Equal(MetricsDrivenPath.Mutation, roundTrip.MetricsPath);
        Assert.Equal(GenerationContextMode.NoHistory, roundTrip.ContextMode);
        Assert.Equal(GenerationBudgetMode.PassAt1RepairAt5, roundTrip.BudgetMode);
        Assert.Equal("ablated-Scenario", roundTrip.AblationVariantId);
        Assert.Equal("{\"approach\":\"MetricsDriven\"}", roundTrip.EffectiveProfileJson);
        Assert.Equal("profile-hash", roundTrip.EffectiveProfileHash);
        Assert.True(roundTrip.IsRepairAttempt);
        Assert.Equal(9, roundTrip.ParentAttemptId);
        Assert.Equal(0.6, roundTrip.Temperature);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GenerationStepMapping_RoundTripsStatusAndSkipReason()
    {
        var step = new GenerationStep
        {
            GenerationAttemptId = 1,
            StepType = GenerationStepType.Scenario,
            Status = GenerationStepStatus.Fallback,
            SkipReason = "disabled by ablation",
            Prompt = "",
            Response = "",
            Success = true,
            StartedAt = DateTime.UtcNow,
            RuleDecisionJson = "[]"
        };

        var roundTrip = step.ToEntity().ToDomain();

        Assert.Equal(GenerationStepStatus.Fallback, roundTrip.Status);
        Assert.Equal("disabled by ablation", roundTrip.SkipReason);
        Assert.Equal("[]", roundTrip.RuleDecisionJson);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TestExecutionMapping_PreservesExplicitClassificationAndValidationMetadata()
    {
        var execution = new ExperimentTestExecution
        {
            GenerationAttemptId = 1,
            TestPassed = false,
            CoverageImprovement = 0.2,
            Classification = TestClassification.FailedEvidencePositive,
            ValidationResultJson = "{\"CoverageImproved\":true}",
            ValidationRuleDecisionJson = "[]",
            ClassificationRuleDecisionJson = "[]",
            ExecutedAt = DateTime.UtcNow
        };

        var roundTrip = execution.ToEntity().ToDomain();

        Assert.Equal(TestClassification.FailedEvidencePositive, roundTrip.Classification);
        Assert.Equal("{\"CoverageImproved\":true}", roundTrip.ValidationResultJson);
        Assert.Equal("[]", roundTrip.ValidationRuleDecisionJson);
        Assert.Equal("[]", roundTrip.ClassificationRuleDecisionJson);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TestExecutionMapping_RejectsOldOutcomeLabels()
    {
        var entity = new TestMap.Persistence.Ef.Entities.Experiment.TestExecutionEntity
        {
            GenerationAttemptId = 1,
            TestClassification = "Approved",
            ExecutionTime = DateTime.UtcNow
        };

        var ex = Assert.Throws<InvalidOperationException>(() => entity.ToDomain());

        Assert.Contains("Old classification labels are not supported", ex.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TestExecutionMapping_DoesNotPersistPassedWhenCompilationOrExecutionFailed()
    {
        var execution = new ExperimentTestExecution
        {
            GenerationAttemptId = 1,
            CompilationSuccess = false,
            TestsExecuted = true,
            TestPassed = true,
            ExecutedAt = DateTime.UtcNow
        };

        var roundTrip = execution.ToEntity().ToDomain();

        Assert.False(roundTrip.CompilationSuccess);
        Assert.False(roundTrip.TestsExecuted);
        Assert.False(roundTrip.TestPassed);
    }
}
