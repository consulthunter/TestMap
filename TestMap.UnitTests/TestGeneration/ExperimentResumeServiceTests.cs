using TestMap.Models.Configuration;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.TestGeneration;

public sealed class ExperimentResumeServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildStableKey_IsDeterministicForSameInputs()
    {
        var service = new ExperimentResumeService();
        var candidate = new CandidateMethod { MemberId = 123 };
        var matrixItem = new GenerationExperimentMatrixItem
        {
            VariantId = "v1",
            Provider = AiProvider.OpenAi,
            Approach = TestGenerationApproach.MetricsDriven,
            MetricsPath = MetricsDrivenPath.Coverage,
            ContextMode = GenerationContextMode.NoHistory,
            BudgetMode = GenerationBudgetMode.PassAt1,
            Steps = new GenerationStepConfig { VariantId = "baseline" },
            Temperature = 0
        };

        var first = service.BuildStableKey(
            "run",
            "owner/repo",
            "abc",
            TestGenerationObjective.TestSuiteExpansion,
            candidate,
            matrixItem);
        var second = service.BuildStableKey(
            "run",
            "owner/repo",
            "abc",
            TestGenerationObjective.TestSuiteExpansion,
            candidate,
            matrixItem);

        Assert.Equal(first, second);
        Assert.Contains("owner/repo", first);
        Assert.Contains("123", first);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_SkipsCompletedWorkItem()
    {
        var service = new ExperimentResumeService();

        var decision = service.Evaluate(
            new ExperimentMatrixWorkItem
            {
                StableKey = "key",
                Status = ExperimentMatrixWorkItemStatus.Completed,
                CreatedAt = DateTime.UtcNow
            },
            new ExperimentResumeConfig { Enabled = true },
            DateTime.UtcNow);

        Assert.False(decision.ShouldExecute);
        Assert.Contains(decision.RuleDecisions, x => x.Value == "ResumeCompletedItemSkipped");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_ResetsStaleRunningWorkItem()
    {
        var service = new ExperimentResumeService();

        var decision = service.Evaluate(
            new ExperimentMatrixWorkItem
            {
                StableKey = "key",
                Status = ExperimentMatrixWorkItemStatus.Running,
                StartedAt = DateTime.UtcNow.AddHours(-2),
                LastHeartbeatAt = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new ExperimentResumeConfig
            {
                Enabled = true,
                RunningAttemptTimeoutMinutes = 30
            },
            DateTime.UtcNow);

        Assert.True(decision.ShouldExecute);
        Assert.Equal(ExperimentMatrixWorkItemStatus.Pending, decision.WorkItem.Status);
        Assert.Contains(decision.RuleDecisions, x => x.Value == "ResumeStaleRunningReset");
    }
}
