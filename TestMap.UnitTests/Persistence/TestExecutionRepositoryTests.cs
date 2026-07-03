using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestExecutionDomain = TestMap.Models.Experiment.TestExecution;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="TestExecutionRepository"/> using an in-memory SQLite database.
/// Covers basic CRUD, the FK-chained filter queries (GetPassedExecutionsAsync,
/// GetExecutionsByClassificationAsync), the statistics aggregation, and the
/// classification distribution grouping.
/// </summary>
public sealed class TestExecutionRepositoryTests
{
    /// <summary>
    /// InsertAsync persists the execution and returns a positive auto-assigned ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_PersistsExecution_ReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestExecutionRepository(db);
        var (_, attemptId) = await SeedFullGraphAsync(db);

        var id = await repo.InsertAsync(MakeDomainExecution(attemptId, passed: true));

        Assert.True(id > 0);
        Assert.Equal(1, await db.TestExecutions.CountAsync());
    }

    /// <summary>
    /// GetByAttemptIdAsync returns the execution linked to the given generation attempt ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByAttemptIdAsync_ReturnsMatchingExecution()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestExecutionRepository(db);
        var (_, attemptId) = await SeedFullGraphAsync(db);
        var executionId = await repo.InsertAsync(MakeDomainExecution(attemptId, passed: true));

        var result = await repo.GetByAttemptIdAsync(attemptId);

        Assert.NotNull(result);
        Assert.Equal(executionId, result!.Id);
        Assert.Equal(attemptId, result.GenerationAttemptId);
    }

    /// <summary>
    /// GetPassedExecutionsAsync returns only executions where the entity TestPassed flag is
    /// true and the candidate method belongs to the given experiment run.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPassedExecutionsAsync_ReturnsOnlyPassedForExperimentRun()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestExecutionRepository(db);

        var (runId, passedAttemptId) = await SeedFullGraphAsync(db);
        var (_, failedAttemptId) = await SeedFullGraphAsync(db);       // different run
        var (_, otherRunAttemptId) = await SeedFullGraphAsync(db);

        db.TestExecutions.Add(MakeEntity(passedAttemptId, passed: true));
        db.TestExecutions.Add(MakeEntity(failedAttemptId, passed: false));
        db.TestExecutions.Add(MakeEntity(otherRunAttemptId, passed: true));  // other experiment run
        await db.SaveChangesAsync();

        var passed = await repo.GetPassedExecutionsAsync(runId);

        // Only the execution in the target run with TestPassed=true should be returned.
        Assert.Single(passed);
        Assert.Equal(passedAttemptId, passed[0].GenerationAttemptId);
        Assert.True(passed[0].TestPassed);
    }

    /// <summary>
    /// GetExecutionsByClassificationAsync filters by the TestClassification string stored on
    /// the entity, restricted to the specified experiment run.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetExecutionsByClassificationAsync_FiltersOnClassification()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestExecutionRepository(db);

        var (runId, attempt1) = await SeedFullGraphAsync(db);
        var (_, attempt2) = await SeedFullGraphAsync(db);

        db.TestExecutions.Add(MakeEntity(attempt1, passed: true,
            classification: "ValidatedEvidencePositive"));
        db.TestExecutions.Add(MakeEntity(attempt2, passed: false,
            classification: "ValidationFailed"));
        await db.SaveChangesAsync();

        var evidencePositive = await repo.GetExecutionsByClassificationAsync(
            runId, TestClassification.ValidatedEvidencePositive);

        Assert.Single(evidencePositive);
        Assert.Equal(TestClassification.ValidatedEvidencePositive,
            evidencePositive[0].Classification);
    }

    /// <summary>
    /// GetExecutionStatisticsAsync correctly counts totals, passed executions, and
    /// computes the pass rate for executions scoped to an experiment run.
    /// Two attempts in the target run (1 passed, 1 failed); one in a different run that
    /// is excluded from the count.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetExecutionStatisticsAsync_ComputesCountsAndPassRate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestExecutionRepository(db);

        // Two attempts in the same target run, one in a separate run
        var (runId, attempt1) = await SeedFullGraphAsync(db);
        var attempt2 = await SeedExtraAttemptAsync(db, runId);
        var (_, attempt3) = await SeedFullGraphAsync(db);  // different run — excluded

        db.TestExecutions.Add(MakeEntity(attempt1, passed: true));
        db.TestExecutions.Add(MakeEntity(attempt2, passed: false));
        db.TestExecutions.Add(MakeEntity(attempt3, passed: true));
        await db.SaveChangesAsync();

        var stats = await repo.GetExecutionStatisticsAsync(runId);

        Assert.Equal(2, stats.TotalExecutions);
        Assert.Equal(1, stats.PassedTests);
        Assert.Equal(0.5, stats.PassRate, precision: 5);
    }

    /// <summary>
    /// GetClassificationDistributionAsync groups executions by their classification value
    /// and returns the correct count for each group. Three attempts in the same run with
    /// two different classification labels.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetClassificationDistributionAsync_GroupsByClassification()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestExecutionRepository(db);

        // Three attempts in the same target run
        var (runId, attempt1) = await SeedFullGraphAsync(db);
        var attempt2 = await SeedExtraAttemptAsync(db, runId);
        var attempt3 = await SeedExtraAttemptAsync(db, runId);

        db.TestExecutions.Add(MakeEntity(attempt1, passed: true,
            classification: "ValidatedLowImpact"));
        db.TestExecutions.Add(MakeEntity(attempt2, passed: true,
            classification: "ValidatedLowImpact"));
        db.TestExecutions.Add(MakeEntity(attempt3, passed: false,
            classification: "ValidationFailed"));
        await db.SaveChangesAsync();

        var distribution = await repo.GetClassificationDistributionAsync(runId);

        Assert.Equal(2, distribution[TestClassification.ValidatedLowImpact]);
        Assert.Equal(1, distribution[TestClassification.ValidationFailed]);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    /// <summary>
    /// Inserts ExperimentRun → CandidateMethod → GenerationAttempt and returns
    /// (experimentRunId, attemptId).
    /// </summary>
    private static async Task<(int ExperimentRunId, int AttemptId)> SeedFullGraphAsync(
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
            SourceMemberId = 10,
            SourceMethodName = "Calculate",
            SourceMethodSignature = "public int Calculate()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();

        var attempt = new GenerationAttemptEntity
        {
            CandidateMethodId = candidate.Id,
            ProviderName = "OpenAi",
            ModelName = "gpt-4o",
            Strategy = string.Empty,
            BudgetMode = "PassAt1",
            Objective = "TestSuiteExpansion",
            GenerationApproach = "MetricsDriven",
            ContextMode = "ChainedHistory",
            StartTime = DateTime.UtcNow,
            Status = "Completed",
            FailureKind = "None",
            FailureStage = string.Empty,
            FailureCategory = string.Empty,
            ErrorMessage = string.Empty,
            StepConfigJson = string.Empty,
            EffectiveProfileJson = string.Empty,
            EffectiveProfileHash = string.Empty,
            RuleDecisionSnapshotJson = string.Empty,
            MetricsPath = string.Empty,
            AblationVariantId = string.Empty
        };
        db.GenerationAttempts.Add(attempt);
        await db.SaveChangesAsync();

        return (run.Id, attempt.Id);
    }

    /// <summary>
    /// Adds a new CandidateMethod + GenerationAttempt to an existing run without creating
    /// a new ExperimentRun. Returns the new attemptId.
    /// </summary>
    private static async Task<int> SeedExtraAttemptAsync(TestMapDbContext db, int experimentRunId)
    {
        var candidate = new CandidateMethodEntity
        {
            ExperimentRunId = experimentRunId,
            SourceMemberId = 20,
            SourceMethodName = "Process",
            SourceMethodSignature = "public void Process()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();

        var attempt = new GenerationAttemptEntity
        {
            CandidateMethodId = candidate.Id,
            ProviderName = "OpenAi",
            ModelName = "gpt-4o",
            Strategy = string.Empty,
            BudgetMode = "PassAt1",
            Objective = "TestSuiteExpansion",
            GenerationApproach = "MetricsDriven",
            ContextMode = "ChainedHistory",
            StartTime = DateTime.UtcNow,
            Status = "Completed",
            FailureKind = "None",
            FailureStage = string.Empty,
            FailureCategory = string.Empty,
            ErrorMessage = string.Empty,
            StepConfigJson = string.Empty,
            EffectiveProfileJson = string.Empty,
            EffectiveProfileHash = string.Empty,
            RuleDecisionSnapshotJson = string.Empty,
            MetricsPath = string.Empty,
            AblationVariantId = string.Empty
        };
        db.GenerationAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return attempt.Id;
    }

    /// <summary>Creates a <see cref="GeneratedTestExecutionEntity"/> to seed directly.</summary>
    private static GeneratedTestExecutionEntity MakeEntity(
        int attemptId,
        bool passed,
        string classification = "ValidatedLowImpact") => new()
    {
        GenerationAttemptId = attemptId,
        CompilationSucceeded = passed,
        TestPassed = passed,
        TestClassification = passed ? classification : "ValidationFailed",
        GeneratedTestCode = string.Empty,
        ValidationResultJson = string.Empty,
        AcceptanceReason = string.Empty,
        StructuredErrors = string.Empty,
        ValidationRuleDecisionSnapshotJson = string.Empty,
        ClassificationRuleDecisionSnapshotJson = string.Empty
    };

    /// <summary>Creates a <see cref="TestExecutionDomain"/> domain object for insert via the repository.</summary>
    private static TestExecutionDomain MakeDomainExecution(int attemptId, bool passed) => new()
    {
        GenerationAttemptId = attemptId,
        CompilationSuccess = passed,
        TestsExecuted = passed,
        TestPassed = passed,
        Classification = passed ? TestClassification.ValidatedLowImpact : TestClassification.ValidationFailed,
        GeneratedTestCode = string.Empty,
        ValidationResultJson = string.Empty,
        AcceptanceReason = null,
        ValidationRuleDecisionSnapshotJson = string.Empty,
        ClassificationRuleDecisionSnapshotJson = string.Empty
    };
}
