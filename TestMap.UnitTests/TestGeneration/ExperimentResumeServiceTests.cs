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
    public void BuildStableKey_DifferentResolvedCommit_ProducesDifferentIdentity()
    {
        var service = new ExperimentResumeService();
        var candidate = new CandidateMethod { MemberId = 123 };
        var matrixItem = new GenerationExperimentMatrixItem
        {
            VariantId = "v1", Provider = AiProvider.OpenAi,
            Approach = TestGenerationApproach.MetricsDriven,
            BudgetMode = GenerationBudgetMode.PassAt1,
            ContextMode = GenerationContextMode.NoHistory,
            Temperature = 0,
            Steps = new GenerationStepConfig { VariantId = "baseline" }
        };
        var first = service.BuildStableKey("run", "owner/repo",
            "0123456789abcdef0123456789abcdef01234567", TestGenerationObjective.TestSuiteExpansion, candidate, matrixItem);
        var second = service.BuildStableKey("run", "owner/repo",
            "89abcdef0123456789abcdef0123456789abcdef", TestGenerationObjective.TestSuiteExpansion, candidate, matrixItem);
        Assert.NotEqual(first, second);
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
    public void BuildStableKey_CohortMemberIdentitySurvivesChangedSourceMemberId()
    {
        var service = new ExperimentResumeService();
        var matrixItem = new GenerationExperimentMatrixItem
        {
            VariantId = "cohort-baseline",
            Provider = AiProvider.CustomOpenAi,
            Approach = TestGenerationApproach.MetricsDriven,
            MetricsPath = MetricsDrivenPath.Coverage,
            ContextMode = GenerationContextMode.NoHistory,
            BudgetMode = GenerationBudgetMode.PassAt1,
            Steps = new GenerationStepConfig { VariantId = "baseline" },
            Temperature = 0
        };

        var first = service.BuildStableKey(
            "series",
            "owner/repo",
            "abc",
            TestGenerationObjective.TestSuiteExpansion,
            new CandidateMethod { MemberId = 10, CandidateCohortMemberId = 7 },
            matrixItem);
        var second = service.BuildStableKey(
            "series",
            "owner/repo",
            "abc",
            TestGenerationObjective.TestSuiteExpansion,
            new CandidateMethod { MemberId = 99, CandidateCohortMemberId = 7 },
            matrixItem);

        Assert.Equal(first, second);
        Assert.Contains("cohort-member:7", first, StringComparison.Ordinal);
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

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildCandidateBaselineKey_FollowsCandidateIdentityNotRowId()
    {
        var original = new CandidateMethod { Id = 1, MemberId = 123 };
        var resumed = new CandidateMethod { Id = 9, MemberId = 123 };
        var other = new CandidateMethod { Id = 2, MemberId = 456 };

        var originalKey = ExperimentResumeService.BuildCandidateBaselineKey("run", "owner/repo", "abc", original);

        Assert.Equal(
            originalKey,
            ExperimentResumeService.BuildCandidateBaselineKey("run", "owner/repo", "abc", resumed));
        Assert.NotEqual(
            originalKey,
            ExperimentResumeService.BuildCandidateBaselineKey("run", "owner/repo", "abc", other));
        Assert.NotEqual(
            originalKey,
            ExperimentResumeService.BuildCandidateBaselineKey("other-run", "owner/repo", "abc", original));
        Assert.EndsWith("|targeted-baseline", originalKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildCandidateBaselineKey_UsesCohortMemberWhenPresent()
    {
        var first = new CandidateMethod { MemberId = 123, CandidateCohortMemberId = 77 };
        var renumbered = new CandidateMethod { MemberId = 321, CandidateCohortMemberId = 77 };

        Assert.Equal(
            ExperimentResumeService.BuildCandidateBaselineKey("run", "owner/repo", "abc", first),
            ExperimentResumeService.BuildCandidateBaselineKey("run", "owner/repo", "abc", renumbered));
    }
}
