using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="GenerationAttemptRepository"/> using an in-memory SQLite database.
/// Covers insert, candidate-scoped queries, the successful-attempts filter (TestPassed),
/// provider success counts, and the update lifecycle.
/// </summary>
public sealed class GenerationAttemptRepositoryTests
{
    /// <summary>
    /// InsertAsync persists the attempt and returns a positive auto-assigned ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_PersistsAttempt_ReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationAttemptRepository(db);
        var (_, candidateMethodId) = await SeedExperimentGraphAsync(db);

        var id = await repo.InsertAsync(MakeAttempt(candidateMethodId: candidateMethodId));

        Assert.True(id > 0);
        Assert.Equal(1, await db.GenerationAttempts.CountAsync());
    }

    /// <summary>
    /// GetByCandidateMethodIdAsync returns attempts for the given method ordered by
    /// StartTime ascending.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByCandidateMethodIdAsync_ReturnsAttemptsOrderedByStartTime()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationAttemptRepository(db);
        var (_, candidateMethodId) = await SeedExperimentGraphAsync(db);
        var (_, otherMethodId) = await SeedExperimentGraphAsync(db);

        await repo.InsertAsync(MakeAttempt(candidateMethodId: candidateMethodId,
            startedAt: new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)));
        await repo.InsertAsync(MakeAttempt(candidateMethodId: candidateMethodId,
            startedAt: new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc)));
        await repo.InsertAsync(MakeAttempt(candidateMethodId: otherMethodId));

        var attempts = await repo.GetByCandidateMethodIdAsync(candidateMethodId: candidateMethodId);

        Assert.Equal(2, attempts.Count);
        Assert.True(attempts[0].StartedAt <= attempts[1].StartedAt);
    }

    /// <summary>
    /// GetSuccessfulAttemptsAsync returns only attempts that have a linked test execution
    /// where TestPassed is true and whose candidate method belongs to the given experiment run.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetSuccessfulAttemptsAsync_FiltersOnTestPassedAndExperimentRun()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationAttemptRepository(db);

        // Seed: experiment run → candidate method → attempts → executions
        var (experimentRunId, candidateMethodId) = await SeedExperimentGraphAsync(db);
        var passedAttemptId = await repo.InsertAsync(MakeAttempt(candidateMethodId: candidateMethodId));
        db.TestExecutions.Add(MakeExecution(passedAttemptId, testPassed: true));
        var failedAttemptId = await repo.InsertAsync(MakeAttempt(candidateMethodId: candidateMethodId));
        db.TestExecutions.Add(MakeExecution(failedAttemptId, testPassed: false));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var successful = await repo.GetSuccessfulAttemptsAsync(experimentRunId);

        // One attempt returned: the one with TestPassed=true, not the failed one.
        var ids = string.Join(",", successful.Select(x => x.Id));
        Assert.True(successful.Count == 1, $"Expected 1 successful attempt, got {successful.Count}. IDs={ids}. passedAttemptId={passedAttemptId}");
        Assert.True(successful[0].Id == passedAttemptId, $"Expected id={passedAttemptId}, got id={successful[0].Id}");
        // TestExecution is eagerly loaded via Include.
        Assert.True(successful[0].TestExecution != null, $"TestExecution was null for attempt {successful[0].Id}");
    }

    /// <summary>
    /// GetSuccessCountByProviderAsync groups successful attempts by provider name and
    /// returns the correct counts for each provider.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetSuccessCountByProviderAsync_CountsByProvider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationAttemptRepository(db);
        var (experimentRunId, candidateMethodId) = await SeedExperimentGraphAsync(db);

        await SeedPassedAttemptAsync(db, repo, candidateMethodId, AiProvider.OpenAi);
        await SeedPassedAttemptAsync(db, repo, candidateMethodId, AiProvider.OpenAi);
        await SeedPassedAttemptAsync(db, repo, candidateMethodId, AiProvider.GoogleGemini);

        var counts = await repo.GetSuccessCountByProviderAsync(experimentRunId);

        Assert.Equal(2, counts[AiProvider.OpenAi]);
        Assert.Equal(1, counts[AiProvider.GoogleGemini]);
    }

    /// <summary>
    /// UpdateAsync patches the tracked entity so subsequent reads reflect the new values.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_ModifiesExistingAttempt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new GenerationAttemptRepository(db);
        var (_, candidateMethodId) = await SeedExperimentGraphAsync(db);
        var attempt = MakeAttempt(candidateMethodId: candidateMethodId);
        attempt.Id = await repo.InsertAsync(attempt);

        attempt.Status = "Completed";
        attempt.TotalTokensUsed = 1234;
        attempt.ModifiedFilePath = "/repo/tests/WidgetTests.cs";
        attempt.ModifiedFileContents = "public class WidgetTests { }";
        attempt.ModifiedFileSha256 = new string('b', 64);
        attempt.CompletedAt = DateTime.UtcNow;
        await repo.UpdateAsync(attempt);

        var updated = await repo.GetByIdAsync(attempt.Id);
        Assert.NotNull(updated);
        Assert.Equal("Completed", updated!.Status);
        Assert.Equal(1234, updated.TotalTokensUsed);
        Assert.Equal("/repo/tests/WidgetTests.cs", updated.ModifiedFilePath);
        Assert.Equal("public class WidgetTests { }", updated.ModifiedFileContents);
        Assert.Equal(new string('b', 64), updated.ModifiedFileSha256);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    /// <summary>Creates a context on an already-initialised connection without running EnsureCreated.</summary>
    private static TestMapDbContext CreateDbContextOnly(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);

    /// <summary>
    /// Creates an ExperimentRunEntity and a CandidateMethodEntity linked to it.
    /// Returns (experimentRunId, candidateMethodId).
    /// </summary>
    private static async Task<(int ExperimentRunId, int CandidateMethodId)> SeedExperimentGraphAsync(
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

        return (run.Id, candidate.Id);
    }

    private static async Task SeedPassedAttemptAsync(
        TestMapDbContext db,
        GenerationAttemptRepository repo,
        int candidateMethodId,
        AiProvider provider)
    {
        var attempt = MakeAttempt(candidateMethodId: candidateMethodId, provider: provider);
        var attemptId = await repo.InsertAsync(attempt);
        db.TestExecutions.Add(MakeExecution(attemptId, testPassed: true));
        await db.SaveChangesAsync();
    }

    private static GeneratedTestExecutionEntity MakeExecution(int attemptId, bool testPassed) => new()
    {
        GenerationAttemptId = attemptId,
        // CompilationSucceeded must be true for the ToDomain mapping to set TestPassed = true.
        // The domain mapping computes: TestPassed = CompilationSucceeded && InferTestsExecuted && entity.TestPassed
        CompilationSucceeded = testPassed,
        TestPassed = testPassed,
        TestClassification = testPassed ? "ValidatedLowImpact" : "ValidationFailed",
        GeneratedTestCode = string.Empty,
        ValidationResultJson = string.Empty,
        AcceptanceReason = string.Empty,
        StructuredErrors = string.Empty,
        ValidationRuleDecisionSnapshotJson = string.Empty,
        ClassificationRuleDecisionSnapshotJson = string.Empty
    };

    private static GenerationAttempt MakeAttempt(
        int candidateMethodId = 1,
        AiProvider provider = AiProvider.OpenAi,
        DateTime? startedAt = null) => new()
    {
        CandidateMethodId = candidateMethodId,
        Provider = provider,
        ModelName = "gpt-4o",
        BudgetMode = GenerationBudgetMode.PassAt1,
        Objective = TestGenerationObjective.TestSuiteExpansion,
        GenerationApproach = TestGenerationApproach.MetricsDriven,
        ContextMode = GenerationContextMode.ChainedHistory,
        StartedAt = startedAt ?? DateTime.UtcNow,
        AttemptNumber = 1,
        Status = "Running"
    };
}
