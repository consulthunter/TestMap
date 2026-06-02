using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.FlakyTestDetection;
using TestMap.Models.Testing;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Testing;
using TestMap.Persistence.Ef.Repositories.Testing;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="TestResultRepository"/> using an in-memory SQLite database.
/// Covers insertion, the cross-join HasPassingTestsAsync query, and the GetHistoryAsync
/// logic that orders, limits, and maps StackTrace directly (bypassing ToDomain).
/// </summary>
public sealed class TestResultRepositoryTests
{
    /// <summary>
    /// InsertAsync persists all supplied results linked to the given test run ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_PersistsAllResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestResultRepository(db);

        var runId = await InsertTestRunAsync(db, projectId: 1);
        var results = new[]
        {
            new TestResultModel { TestName = "Test_A", Outcome = "Passed", RunId = "r1" },
            new TestResultModel { TestName = "Test_B", Outcome = "Failed", RunId = "r1" }
        };
        await repo.InsertAsync(results, testRunId: runId);

        Assert.Equal(2, await db.TestResults.CountAsync());
        Assert.All(await db.TestResults.ToListAsync(), r => Assert.Equal(runId, r.TestRunId));
    }

    /// <summary>
    /// HasPassingTestsAsync returns false when no test runs exist for the project.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HasPassingTestsAsync_ReturnsFalse_WhenNoRunsExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestResultRepository(db);

        var result = await repo.HasPassingTestsAsync(projectId: 1);

        Assert.False(result);
    }

    /// <summary>
    /// HasPassingTestsAsync returns false when runs exist but all results have a non-passing outcome.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HasPassingTestsAsync_ReturnsFalse_WhenAllResultsFailed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestResultRepository(db);
        var runId = await InsertTestRunAsync(db, projectId: 2);

        await repo.InsertAsync(
            [new TestResultModel { TestName = "BadTest", Outcome = "Failed" }],
            testRunId: runId);

        Assert.False(await repo.HasPassingTestsAsync(projectId: 2));
    }

    /// <summary>
    /// HasPassingTestsAsync returns true when at least one result has a "passed" outcome
    /// (case-insensitive) linked to a run belonging to the given project.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HasPassingTestsAsync_ReturnsTrue_WhenPassingResultExists()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestResultRepository(db);
        var runId = await InsertTestRunAsync(db, projectId: 3);

        await repo.InsertAsync(
            [new TestResultModel { TestName = "GoodTest", Outcome = "Passed" }],
            testRunId: runId);

        Assert.True(await repo.HasPassingTestsAsync(projectId: 3));
    }

    /// <summary>
    /// GetHistoryAsync returns results in chronological order (oldest first) and respects
    /// the historyWindowRuns limit.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetHistoryAsync_ReturnsResultsChronologicallyAndRespectLimit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestResultRepository(db);

        // Three runs in reverse insertion order so ordering by CreatedAt is non-trivial
        var runIds = new int[3];
        for (var i = 0; i < 3; i++)
            runIds[i] = await InsertTestRunAsync(db, projectId: 4,
                createdAt: new DateTime(2026, 1, i + 1, 0, 0, 0, DateTimeKind.Utc));

        foreach (var runId in runIds)
            await repo.InsertAsync([new TestResultModel
            {
                TestName = "SomeTest",
                Outcome = "Passed",
                RunId = $"run_{runId}"
            }], testRunId: runId);

        var identity = new TestExecutionResultModel { TestName = "SomeTest" };
        var history = await repo.GetHistoryAsync(identity, historyWindowRuns: 2);

        // Only the 2 most-recent runs, returned oldest-first
        Assert.Equal(2, history.Count);
        Assert.True(history[0].CreatedAt <= history[1].CreatedAt);
    }

    /// <summary>
    /// When TestMemberId is provided, GetHistoryAsync filters by MethodId rather than TestName.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetHistoryAsync_FiltersByMemberId_WhenSet()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestResultRepository(db);
        var runId = await InsertTestRunAsync(db, projectId: 5);

        await repo.InsertAsync([
            new TestResultModel { TestName = "Test_Matching", Outcome = "Passed", MethodId = 10 },
            new TestResultModel { TestName = "Test_Other",    Outcome = "Passed", MethodId = 99 }
        ], testRunId: runId);

        var identity = new TestExecutionResultModel { TestMemberId = 10 };
        var history = await repo.GetHistoryAsync(identity, historyWindowRuns: 10);

        Assert.Single(history);
        Assert.Equal(10, history[0].TestMemberId);
    }

    /// <summary>
    /// GetHistoryAsync maps the StackTrace column directly to ErrorStackTrace on the returned
    /// model — this is a manual mapping that bypasses the standard ToDomain extension.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetHistoryAsync_MapsStackTraceToErrorStackTrace()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestResultRepository(db);
        var runId = await InsertTestRunAsync(db, projectId: 6);

        db.TestResults.Add(new TestResultEntity
        {
            TestRunId = runId,
            RunId = "r",
            TestName = "Failing",
            Outcome = "Failed",
            Duration = TimeSpan.Zero,
            ErrorMessage = "Assert failed",
            StackTrace = "at MyTest() in File.cs:42"
        });
        await db.SaveChangesAsync();

        var identity = new TestExecutionResultModel { TestName = "Failing" };
        var history = await repo.GetHistoryAsync(identity, historyWindowRuns: 10);

        var record = Assert.Single(history);
        Assert.Equal("at MyTest() in File.cs:42", record.ErrorStackTrace);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<int> InsertTestRunAsync(
        TestMapDbContext db,
        int projectId,
        DateTime? createdAt = null)
    {
        var entity = new TestRunEntity
        {
            ProjectId = projectId,
            RunId = $"run_{Guid.NewGuid():N}",
            RunDate = "2026-06-01",
            Success = true,
            Coverage = 0,
            LogPath = string.Empty,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.TestRuns.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }
}
