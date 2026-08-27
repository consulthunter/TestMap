using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Experiment.Assertions;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment.Assertions;
using TestMap.Services.Experiment.Reporting;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GeneratedTestAssertionLineageTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ExecutionMapping_KeepsAssertionEvidenceTransientAndPreservesDynamicMetrics()
    {
        var execution = new TestMap.Models.Experiment.TestExecution
        {
            GenerationAttemptId = 7,
            MemberId = 19,
            GeneratedTestCode = "[Fact] public void Generated() { }",
            GeneratedTestMethodName = "Generated",
            CompilationSuccess = true,
            TestsExecuted = true,
            TestPassed = true,
            CoverageAfter = 0.75,
            CoverageImprovement = 0.05,
            BaselineMutationScore = 40,
            MutationScoreAfter = 55,
            MutationScoreImprovement = 15,
            Classification = TestClassification.ValidatedEvidencePositive,
            Accepted = true,
            AssertionLineageAnalysis = new AssertionLineageAnalysisResult
            {
                Available = true,
                Policy = new AssertionLineagePolicy(),
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                TestSummaries = []
            }
        };

        var roundTrip = execution.ToEntity().ToDomain();

        Assert.Null(roundTrip.AssertionLineageAnalysis);
        Assert.Equal(execution.Accepted, roundTrip.Accepted);
        Assert.Equal(execution.Classification, roundTrip.Classification);
        Assert.Equal(execution.CoverageAfter, roundTrip.CoverageAfter);
        Assert.Equal(execution.CoverageImprovement, roundTrip.CoverageImprovement);
        Assert.Equal(execution.MutationScoreAfter, roundTrip.MutationScoreAfter);
        Assert.Equal(execution.MutationScoreImprovement, roundTrip.MutationScoreImprovement);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PostAnalysisPersistence_AssignsPersistedExecutionOwnerBeforeUnavailableSnapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var (runId, candidateId, attemptId) = await SeedGenerationAttemptAsync(db);
        var execution = new GeneratedTestExecutionEntity
        {
            GenerationAttemptId = attemptId,
            MemberId = null,
            GeneratedTestCode = "[Fact] public void Generated() { Assert.True(true); }",
            GeneratedTestMethodName = "Generated",
            CompilationSucceeded = true,
            TestPassed = true,
            TestClassification = TestClassification.ValidatedLowImpact.ToString(),
            ValidationResultJson = "{}",
            AcceptanceReason = "Accepted.",
            ValidationRuleDecisionSnapshotJson = "[]",
            ClassificationRuleDecisionSnapshotJson = "[]",
            StructuredErrors = string.Empty,
            ExecutionTime = DateTime.UtcNow
        };
        db.TestExecutions.Add(execution);
        await db.SaveChangesAsync();

        var measurement = new AssertionLineageMeasurement
        {
            ProjectId = 1,
            ExperimentRunId = runId,
            CandidateMethodId = candidateId,
            GenerationAttemptId = attemptId,
            ProducerLane = "testmap",
            Status = AssertionMeasurementStatus.Unavailable,
            FailureCode = AssertionLineageReasonCodes.GeneratedTestMemberUnresolved,
            FailureReason = "The generated test member could not be linked after refresh.",
            PolicyVersion = AssertionLineagePolicy.CurrentPolicyVersion,
            AssertionCatalogVersion = AssertionLineagePolicy.CurrentCatalogVersion,
            MaxDepth = 4,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            TestSummaries =
            [
                new GeneratedTestAssertionSummary
                {
                    GeneratedTestExecutionId = execution.Id,
                    TestMethodName = "Generated",
                    TestFilePath = "WidgetTests.cs",
                    TestMemberContentHash = new string('a', 64),
                    FallbackIdentityHash = new string('b', 64),
                    Status = GeneratedTestAssertionStatus.Unavailable,
                    StatusReason =
                        $"{AssertionLineageReasonCodes.GeneratedTestMemberUnresolved}: no persisted member"
                }
            ]
        };
        var repository = new AssertionLineageMeasurementRepository(
            db,
            new AssertionLineageSummaryService(),
            new AssertionLineageAuditService());

        var id = await repository.InsertAsync(measurement);
        db.ChangeTracker.Clear();
        var persisted = await repository.GetByIdAsync(id);

        Assert.NotNull(persisted);
        Assert.Equal(AssertionMeasurementStatus.Unavailable, persisted!.Status);
        Assert.Null(persisted.RecognizedAssertionCount);
        Assert.Equal(execution.Id, Assert.Single(persisted.TestSummaries).GeneratedTestExecutionId);
        Assert.Equal(
            GeneratedTestAssertionStatus.Unavailable,
            persisted.TestSummaries[0].Status);
    }

    private static async Task<(int RunId, int CandidateId, int AttemptId)> SeedGenerationAttemptAsync(
        TestMapDbContext db)
    {
        var run = new ExperimentRunEntity
        {
            ProjectId = 1,
            StartTime = DateTime.UtcNow,
            Objective = "TestSuiteExpansion",
            CandidateSelectionStrategy = "Existing",
            Configuration = "{}",
            ResultsFilePath = string.Empty,
            Status = "Running"
        };
        db.ExperimentRuns.Add(run);
        await db.SaveChangesAsync();
        var candidate = new CandidateMethodEntity
        {
            ExperimentRunId = run.Id,
            SourceMemberId = 11,
            SourceMethodName = "Calculate",
            SourceMethodSignature = "int Calculate()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();
        var attemptId = await new GenerationAttemptRepository(db).InsertAsync(new GenerationAttempt
        {
            CandidateMethodId = candidate.Id,
            Provider = AiProvider.OpenAi,
            ModelName = "gpt",
            BudgetMode = GenerationBudgetMode.PassAt1,
            Objective = TestGenerationObjective.TestSuiteExpansion,
            GenerationApproach = TestGenerationApproach.MetricsDriven,
            ContextMode = GenerationContextMode.ChainedHistory,
            StartedAt = DateTime.UtcNow,
            AttemptNumber = 1,
            Status = "Completed"
        });
        return (run.Id, candidate.Id, attemptId);
    }
}
