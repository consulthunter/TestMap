using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.FlakyTestDetection;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="FlakyTestRerunResultRepository"/> using an in-memory SQLite database.
/// Covers bulk insert and the GetByRunIdAsync query that orders by TestResultId then
/// AttemptNumber — preserving the chronological rerun sequence.
/// </summary>
public sealed class FlakyTestRerunResultRepositoryTests
{
    /// <summary>
    /// BulkInsertAsync inserts all supplied results in a single save.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BulkInsertAsync_InsertsAllResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new FlakyTestRerunResultRepository(db);

        await repo.BulkInsertAsync([
            MakeResult(runId: "run-001", testResultId: 1, attempt: 1, outcome: "Passed"),
            MakeResult(runId: "run-001", testResultId: 1, attempt: 2, outcome: "Failed"),
            MakeResult(runId: "run-001", testResultId: 2, attempt: 1, outcome: "Passed")
        ]);

        Assert.Equal(3, await db.FlakyTestRerunResults.CountAsync());
    }

    /// <summary>
    /// GetByRunIdAsync returns only results for the given RunId, ordered by
    /// TestResultId ascending then AttemptNumber ascending.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByRunIdAsync_ReturnsResultsOrderedByResultIdThenAttempt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new FlakyTestRerunResultRepository(db);

        // Insert in scrambled order
        await repo.BulkInsertAsync([
            MakeResult("run-x", testResultId: 2, attempt: 2, outcome: "Passed"),
            MakeResult("run-x", testResultId: 1, attempt: 1, outcome: "Failed"),
            MakeResult("run-x", testResultId: 1, attempt: 2, outcome: "Passed"),
            MakeResult("run-y", testResultId: 1, attempt: 1, outcome: "Failed")  // other run
        ]);

        var results = await repo.GetByRunIdAsync("run-x");

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal("run-x", r.RunId));

        // Primary sort: TestResultId asc
        Assert.Equal(1, results[0].TestResultId);
        Assert.Equal(1, results[1].TestResultId);
        Assert.Equal(2, results[2].TestResultId);

        // Secondary sort within same TestResultId: AttemptNumber asc
        Assert.Equal(1, results[0].AttemptNumber);
        Assert.Equal(2, results[1].AttemptNumber);
    }

    /// <summary>
    /// GetByRunIdAsync returns an empty list when no results exist for the given RunId.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByRunIdAsync_ReturnsEmpty_WhenRunIdNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new FlakyTestRerunResultRepository(db);

        var results = await repo.GetByRunIdAsync("no-such-run");

        Assert.Empty(results);
    }

    /// <summary>
    /// Outcome, ErrorMessage, and DurationMs are persisted and round-trip correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BulkInsertAsync_FieldsRoundtripCorrectly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new FlakyTestRerunResultRepository(db);

        await repo.BulkInsertAsync([
            MakeResult("run-rt", testResultId: 10, attempt: 1, outcome: "Failed",
                errorMessage: "Assert.Equal failed", durationMs: 42.5)
        ]);

        var results = await repo.GetByRunIdAsync("run-rt");
        var single = Assert.Single(results);
        Assert.Equal("Failed", single.Outcome);
        Assert.Equal("Assert.Equal failed", single.ErrorMessage);
        Assert.Equal(42.5, single.DurationMs, precision: 5);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static FlakyTestRerunResultModel MakeResult(
        string runId,
        int testResultId,
        int attempt,
        string outcome,
        string? errorMessage = null,
        double durationMs = 10.0) => new()
    {
        RunId = runId,
        TestResultId = testResultId,
        AttemptNumber = attempt,
        Outcome = outcome,
        DurationMs = durationMs,
        ErrorMessage = errorMessage
    };
}
