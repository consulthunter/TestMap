using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.AgentTools;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.AgentTools;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="ToolAttemptRepository"/> using an in-memory SQLite database.
/// Covers insert, round-trip fidelity, status-only update, and scoped queries.
/// </summary>
public sealed class ToolAttemptRepositoryTests
{
    /// <summary>
    /// InsertAsync persists the attempt and returns a positive auto-assigned id.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_PersistsAttempt_ReturnsPositiveId()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ToolAttemptRepository(db);
        var (runId, candidateId) = await SeedExperimentGraphAsync(db);

        // Act
        var id = await repo.InsertAsync(MakeAttempt(runId, candidateId));

        // Assert
        Assert.True(id > 0);
        Assert.Equal(1, await db.ToolAttempts.CountAsync());
    }

    /// <summary>
    /// GetByIdAsync returns a domain model that round-trips the same field values as inserted.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByIdAsync_AfterInsert_RoundTripsDomainFields()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ToolAttemptRepository(db);
        var (runId, candidateId) = await SeedExperimentGraphAsync(db);
        var attempt = MakeAttempt(runId, candidateId);
        attempt.ToolId = "codex";
        attempt.RunStatus = ToolRunStatus.Planned;

        var id = await repo.InsertAsync(attempt);

        // Act
        var retrieved = await repo.GetByIdAsync(id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved!.Id);
        Assert.Equal("codex", retrieved.ToolId);
        Assert.Equal(ToolRunStatus.Planned, retrieved.RunStatus);
        Assert.Equal(candidateId, retrieved.CandidateMethodId);
    }

    /// <summary>
    /// UpdateStatusAsync changes only the run_status column; other fields are unchanged.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateStatusAsync_ChangesStatus_OtherFieldsUnchanged()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ToolAttemptRepository(db);
        var (runId, candidateId) = await SeedExperimentGraphAsync(db);
        var attempt = MakeAttempt(runId, candidateId);
        attempt.ToolId = "codex";
        var id = await repo.InsertAsync(attempt);

        // Act
        await repo.UpdateStatusAsync(id, ToolRunStatus.Running);

        // Assert
        db.ChangeTracker.Clear();
        var updated = await repo.GetByIdAsync(id);
        Assert.Equal(ToolRunStatus.Running, updated!.RunStatus);
        Assert.Equal("codex", updated.ToolId); // unchanged
    }

    /// <summary>
    /// GetByCandidateAsync returns attempts for the right candidate ordered by StartedAt.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByCandidateAsync_ReturnsCorrectAttemptsOrderedByStartedAt()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ToolAttemptRepository(db);
        var (run1Id, candidate1Id) = await SeedExperimentGraphAsync(db);
        var (run2Id, candidate2Id) = await SeedExperimentGraphAsync(db);

        await repo.InsertAsync(MakeAttempt(run1Id, candidate1Id,
            startedAt: new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)));
        await repo.InsertAsync(MakeAttempt(run1Id, candidate1Id,
            startedAt: new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc)));
        await repo.InsertAsync(MakeAttempt(run2Id, candidate2Id)); // different candidate

        // Act
        var results = await repo.GetByCandidateAsync(candidate1Id);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.True(results[0].StartedAt <= results[1].StartedAt, "Results should be ordered by StartedAt asc");
        Assert.All(results, a => Assert.Equal(candidate1Id, a.CandidateMethodId));
    }

    /// <summary>
    /// GetByExperimentRunAsync returns only attempts belonging to the given run.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByExperimentRunAsync_ReturnsOnlyAttemptsForThatRun()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ToolAttemptRepository(db);
        var (run1Id, candidate1Id) = await SeedExperimentGraphAsync(db);
        var (run2Id, candidate2Id) = await SeedExperimentGraphAsync(db);

        await repo.InsertAsync(MakeAttempt(run1Id, candidate1Id));
        await repo.InsertAsync(MakeAttempt(run1Id, candidate1Id));
        await repo.InsertAsync(MakeAttempt(run2Id, candidate2Id)); // belongs to run2

        // Act
        var results = await repo.GetByExperimentRunAsync(run1Id);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, a => Assert.Equal(run1Id, a.ExperimentRunId));
    }

    /// <summary>
    /// UpdateAsync persists all field changes and makes them visible via GetByIdAsync.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_PersistsAllChanges()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ToolAttemptRepository(db);
        var (runId, candidateId) = await SeedExperimentGraphAsync(db);
        var attempt = MakeAttempt(runId, candidateId);
        attempt.Id = await repo.InsertAsync(attempt);

        // Act
        attempt.RunStatus = ToolRunStatus.Completed;
        attempt.ChangedFilesCount = 5;
        attempt.ElapsedSeconds = 120.5;
        attempt.StdOutLogPath = "/attempt/codex.events.jsonl";
        attempt.StdErrLogPath = "/attempt/codex.stderr.log";
        attempt.JsonlLogPath = "/attempt/codex.events.jsonl";
        attempt.CompletedAt = DateTime.UtcNow;
        await repo.UpdateAsync(attempt);

        // Assert
        db.ChangeTracker.Clear();
        var updated = await repo.GetByIdAsync(attempt.Id);
        Assert.Equal(ToolRunStatus.Completed, updated!.RunStatus);
        Assert.Equal(5, updated.ChangedFilesCount);
        Assert.Equal(120.5, updated.ElapsedSeconds);
        Assert.Equal("/attempt/codex.events.jsonl", updated.StdOutLogPath);
        Assert.Equal("/attempt/codex.stderr.log", updated.StdErrLogPath);
        Assert.Equal("/attempt/codex.events.jsonl", updated.JsonlLogPath);
        Assert.NotNull(updated.CompletedAt);
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
    /// Seeds an ExperimentRunEntity and a CandidateMethodEntity linked to it.
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

    private static ToolAttempt MakeAttempt(
        int experimentRunId,
        int candidateMethodId,
        DateTime? startedAt = null) => new()
    {
        ExperimentRunId = experimentRunId,
        CandidateMethodId = candidateMethodId,
        ToolId = "codex",
        RunStatus = ToolRunStatus.Planned,
        StartedAt = startedAt ?? DateTime.UtcNow,
        TimeoutSeconds = 2700
    };
}
