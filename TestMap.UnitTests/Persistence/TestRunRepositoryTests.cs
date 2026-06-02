using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Testing;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Testing;
using TestMap.Persistence.Ef.Repositories.Testing;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="TestRunRepository"/> using an in-memory SQLite database.
/// Covers insert/update semantics, baseline querying, and the mapping fix that
/// populates DbId and DbProjectId on domain objects read back from the store.
/// </summary>
public sealed class TestRunRepositoryTests
{
    /// <summary>
    /// Inserting a new run returns a positive ID and the run can be retrieved by that ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_NewRun_ReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestRunRepository(db);

        var id = await repo.InsertOrUpdateAsync(MakeRun("run_001"), projectId: 1);

        Assert.True(id > 0);
        Assert.Equal(1, await db.TestRuns.CountAsync());
    }

    /// <summary>
    /// Calling InsertOrUpdateAsync twice with the same project and RunId does not
    /// create a duplicate row — returns the existing ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_SameRunId_IsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestRunRepository(db);

        var firstId = await repo.InsertOrUpdateAsync(MakeRun("run_001"), projectId: 1);
        var secondId = await repo.InsertOrUpdateAsync(MakeRun("run_001"), projectId: 1);

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await db.TestRuns.CountAsync());
    }

    /// <summary>
    /// When a run with the same RunId exists but fields have changed, InsertOrUpdateAsync
    /// updates the row and returns the same ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_ChangedCoverage_UpdatesRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestRunRepository(db);
        var run = MakeRun("run_001");
        await repo.InsertOrUpdateAsync(run, projectId: 1);

        run.Coverage = 95;
        await repo.InsertOrUpdateAsync(run, projectId: 1);

        var entity = await db.TestRuns.FirstAsync();
        Assert.Equal(95, entity.Coverage);
        Assert.Equal(1, await db.TestRuns.CountAsync());
    }

    /// <summary>
    /// GetLatestBaselineAsync returns the most-recently created run whose RunId starts
    /// with "baseline_", ordered by CreatedAt descending then Id descending.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLatestBaselineAsync_ReturnsMostRecentBaseline()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestRunRepository(db);

        var older = new TestRunEntity
        {
            ProjectId = 5,
            RunId = "baseline_001",
            RunDate = "2026-01-01",
            Success = true,
            Coverage = 60,
            LogPath = string.Empty,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var newer = new TestRunEntity
        {
            ProjectId = 5,
            RunId = "baseline_002",
            RunDate = "2026-06-01",
            Success = true,
            Coverage = 80,
            LogPath = string.Empty,
            CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.TestRuns.AddRange(older, newer);
        await db.SaveChangesAsync();

        var latest = await repo.GetLatestBaselineAsync(projectId: 5);

        Assert.NotNull(latest);
        Assert.Equal("baseline_002", latest!.RunId);
        Assert.Equal(80, latest.Coverage);
    }

    /// <summary>
    /// GetLatestBaselineAsync returns null when no runs with a "baseline_" prefix exist
    /// for the given project.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLatestBaselineAsync_ReturnsNull_WhenNoBaselinesExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestRunRepository(db);

        var result = await repo.GetLatestBaselineAsync(projectId: 99);

        Assert.Null(result);
    }

    /// <summary>
    /// GetAllAsync returns domain models with DbId and DbProjectId correctly populated —
    /// previously ToDomain dropped both fields, leaving them as 0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_ReturnsMappedRunsWithDbIdAndDbProjectId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestRunRepository(db);
        await repo.InsertOrUpdateAsync(MakeRun("run_a"), projectId: 7);
        await repo.InsertOrUpdateAsync(MakeRun("run_b"), projectId: 7);

        var runs = await repo.GetAllAsync();

        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.True(r.DbId > 0));
        Assert.All(runs, r => Assert.Equal(7, r.DbProjectId));
    }

    /// <summary>
    /// GetByRunIdAsync finds a run by its string RunId and returns a mapped domain object.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByRunIdAsync_FindsExistingRun()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestRunRepository(db);
        await repo.InsertOrUpdateAsync(MakeRun("target_run"), projectId: 3);

        var found = await repo.GetByRunIdAsync("target_run");

        Assert.NotNull(found);
        Assert.Equal("target_run", found!.RunId);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static TestRunModel MakeRun(string runId) => new()
    {
        RunId = runId,
        RunDate = "2026-06-01",
        Success = true,
        Coverage = 75,
        LogPath = "/logs/run.log"
    };
}
