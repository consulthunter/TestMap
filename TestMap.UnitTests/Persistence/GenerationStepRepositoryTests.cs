using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="GenerationStepRepository"/> using an in-memory SQLite database.
/// Covers insert, attempt-scoped ordering by StepOrder, step-type lookup,
/// bulk insert, and the aggregate GetAverageTokensByStepTypeAsync cross-join.
/// </summary>
public sealed class GenerationStepRepositoryTests
{
    /// <summary>
    /// InsertAsync persists the step and returns a positive auto-assigned ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_PersistsStep_ReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationStepRepository(db);
        var attemptId = await SeedAttemptAsync(db);

        var id = await repo.InsertAsync(MakeStep(attemptId, GenerationStepType.Scenario, tokens: 100));

        Assert.True(id > 0);
        Assert.Equal(1, await db.GenerationSteps.CountAsync());
    }

    /// <summary>
    /// GetByAttemptIdAsync returns steps for the given attempt ordered by StepOrder ascending,
    /// excluding steps from other attempts.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByAttemptIdAsync_ReturnsStepsOrderedByStepOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationStepRepository(db);
        var attemptId = await SeedAttemptAsync(db);
        var otherAttemptId = await SeedAttemptAsync(db);

        // Insert in reverse order so ordering is non-trivial
        await repo.InsertAsync(MakeStep(attemptId, GenerationStepType.FinalTest));
        await repo.InsertAsync(MakeStep(attemptId, GenerationStepType.Scenario));
        await repo.InsertAsync(MakeStep(otherAttemptId, GenerationStepType.Scenario));

        var steps = await repo.GetByAttemptIdAsync(attemptId);

        Assert.Equal(2, steps.Count);
        // Scenario (int value 1) < FinalTest (int value 10) — StepOrder = (int)StepType
        Assert.True((int)steps[0].StepType <= (int)steps[1].StepType);
    }

    /// <summary>
    /// GetByStepTypeAsync returns the step matching the given attempt ID and step type.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByStepTypeAsync_FindsStepByType()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationStepRepository(db);
        var attemptId = await SeedAttemptAsync(db);

        await repo.InsertAsync(MakeStep(attemptId, GenerationStepType.Scenario));
        await repo.InsertAsync(MakeStep(attemptId, GenerationStepType.FinalTest));

        var step = await repo.GetByStepTypeAsync(attemptId, GenerationStepType.FinalTest);

        Assert.NotNull(step);
        Assert.Equal(GenerationStepType.FinalTest, step!.StepType);
        Assert.Equal(attemptId, step.GenerationAttemptId);
    }

    /// <summary>
    /// BulkInsertAsync inserts all supplied steps in a single save, returning the correct count.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BulkInsertAsync_InsertsAllSteps()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationStepRepository(db);
        var attemptId = await SeedAttemptAsync(db);

        var steps = new List<GenerationStep>
        {
            MakeStep(attemptId, GenerationStepType.EvidencePackage, tokens: 50),
            MakeStep(attemptId, GenerationStepType.Scenario, tokens: 80),
            MakeStep(attemptId, GenerationStepType.FinalTest, tokens: 200)
        };
        await repo.BulkInsertAsync(steps);

        Assert.Equal(3, await db.GenerationSteps.CountAsync());
    }

    /// <summary>
    /// GetAverageTokensByStepTypeAsync computes the per-step-type average token count
    /// across all attempts in the experiment run, keyed by GenerationStepType enum.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAverageTokensByStepTypeAsync_ReturnsAveragePerStepType()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationStepRepository(db);

        // Two attempts in the same experiment run
        var (runId, attempt1) = await SeedGraphAsync(db);
        var (_, attempt2) = await SeedAttemptInRunAsync(db, runId);

        // Scenario: 100 + 200 tokens across the two attempts → average 150
        // FinalTest: only on attempt 1 → average 400
        await repo.BulkInsertAsync([
            MakeStep(attempt1, GenerationStepType.Scenario, tokens: 100),
            MakeStep(attempt1, GenerationStepType.FinalTest, tokens: 400),
            MakeStep(attempt2, GenerationStepType.Scenario, tokens: 200)
        ]);

        var averages = await repo.GetAverageTokensByStepTypeAsync(runId);

        Assert.Equal(150, averages[GenerationStepType.Scenario]);
        Assert.Equal(400, averages[GenerationStepType.FinalTest]);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<int> SeedAttemptAsync(TestMapDbContext db)
    {
        var (_, attemptId) = await SeedGraphAsync(db);
        return attemptId;
    }

    /// <summary>
    /// Creates ExperimentRun → CandidateMethod → GenerationAttempt; returns (runId, attemptId).
    /// </summary>
    private static async Task<(int RunId, int AttemptId)> SeedGraphAsync(TestMapDbContext db)
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

        var (_, attemptId) = await SeedAttemptInRunAsync(db, run.Id, candidate.Id);
        return (run.Id, attemptId);
    }

    private static async Task<(int RunId, int AttemptId)> SeedAttemptInRunAsync(
        TestMapDbContext db, int runId, int? candidateId = null)
    {
        if (candidateId == null)
        {
            var candidate = new CandidateMethodEntity
            {
                ExperimentRunId = runId,
                SourceMemberId = 11,
                SourceMethodName = "Process",
                SourceMethodSignature = "public void Process()"
            };
            db.CandidateMethods.Add(candidate);
            await db.SaveChangesAsync();
            candidateId = candidate.Id;
        }

        var attempt = new GenerationAttemptEntity
        {
            CandidateMethodId = candidateId.Value,
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
        return (runId, attempt.Id);
    }

    private static GenerationStep MakeStep(
        int attemptId,
        GenerationStepType stepType,
        int tokens = 100) => new()
    {
        GenerationAttemptId = attemptId,
        StepType = stepType,
        Status = GenerationStepStatus.Executed,
        Prompt = "prompt",
        Response = "response",
        TokenCount = tokens,
        StartedAt = DateTime.UtcNow,
        Success = true,
        RuleDecisionSnapshotJson = string.Empty
    };
}
