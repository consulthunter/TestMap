using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.FlakyTestDetection;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.FlakyTestDetection;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="FlakyTestScoreRepository"/> using an in-memory SQLite database.
/// Covers single insert, bulk insert, and the GetByRunIdAsync query that orders by
/// FlakinessScore descending.
/// </summary>
public sealed class FlakyTestScoreRepositoryTests
{
    /// <summary>
    /// InsertAsync persists the score and returns a positive auto-assigned ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_PersistsScore_ReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new FlakyTestScoreRepository(db);

        var id = await repo.InsertAsync(MakeScore(runId: "run-001", testName: "Test_A", score: 0.8));

        Assert.True(id > 0);
        Assert.Equal(1, await db.FlakyTestScores.CountAsync());
    }

    /// <summary>
    /// BulkInsertAsync inserts all supplied scores in a single save.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BulkInsertAsync_InsertsAllScores()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new FlakyTestScoreRepository(db);

        await repo.BulkInsertAsync([
            MakeScore("run-002", "Test_A", 0.3),
            MakeScore("run-002", "Test_B", 0.7),
            MakeScore("run-002", "Test_C", 0.5)
        ]);

        Assert.Equal(3, await db.FlakyTestScores.CountAsync());
    }

    /// <summary>
    /// GetByRunIdAsync returns only the scores for the given RunId, ordered by
    /// FlakinessScore descending (highest risk first).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByRunIdAsync_ReturnsScoresForRunOrderedByScoreDesc()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new FlakyTestScoreRepository(db);

        await repo.BulkInsertAsync([
            MakeScore("run-a", "LowFlaky", score: 0.2),
            MakeScore("run-a", "HighFlaky", score: 0.9),
            MakeScore("run-a", "MidFlaky", score: 0.5),
            MakeScore("run-b", "OtherRun", score: 1.0)  // different run — excluded
        ]);

        var scores = await repo.GetByRunIdAsync("run-a");

        Assert.Equal(3, scores.Count);
        // Ordered by FlakinessScore desc
        Assert.True(scores[0].FlakinessScore >= scores[1].FlakinessScore);
        Assert.True(scores[1].FlakinessScore >= scores[2].FlakinessScore);
        Assert.All(scores, s => Assert.Equal("run-a", s.RunId));
    }

    /// <summary>
    /// GetByRunIdAsync returns an empty list when no scores exist for the given RunId.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByRunIdAsync_ReturnsEmpty_WhenRunIdNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new FlakyTestScoreRepository(db);

        var scores = await repo.GetByRunIdAsync("nonexistent-run");

        Assert.Empty(scores);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static FlakyTestScoreModel MakeScore(
        string runId,
        string testName,
        double score) => new()
    {
        RunId = runId,
        TestName = testName,
        FilePath = $"/tests/{testName}.cs",
        FlakinessScore = score,
        Classification = FlakyTestClassification.InsufficientData
    };
}
