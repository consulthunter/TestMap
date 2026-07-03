using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.RiskScoring;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="CandidateMethodRiskScoreRepository"/> using an in-memory SQLite database.
/// Covers single insert, bulk insert, candidate-method-scoped query (ordered by RiskScore desc),
/// and member-scoped query (ordered by CreatedAt desc).
/// </summary>
public sealed class CandidateMethodRiskScoreRepositoryTests
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
        var repo = new CandidateMethodRiskScoreRepository(db);

        var id = await repo.InsertAsync(MakeScore(candidateMethodId: 1, memberId: 10, risk: 0.75));

        Assert.True(id > 0);
        Assert.Equal(1, await db.CandidateMethodRiskScores.CountAsync());
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
        var repo = new CandidateMethodRiskScoreRepository(db);

        await repo.BulkInsertAsync([
            MakeScore(candidateMethodId: 2, memberId: 20, risk: 0.3),
            MakeScore(candidateMethodId: 2, memberId: 21, risk: 0.6),
            MakeScore(candidateMethodId: 2, memberId: 22, risk: 0.9)
        ]);

        Assert.Equal(3, await db.CandidateMethodRiskScores.CountAsync());
    }

    /// <summary>
    /// GetByCandidateMethodIdAsync returns only scores for the given candidate method,
    /// ordered by RiskScore descending (highest risk first).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByCandidateMethodIdAsync_ReturnsScoresOrderedByRiskDesc()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CandidateMethodRiskScoreRepository(db);

        // CandidateMethodId is nullable — no FK constraint is enforced.
        await repo.BulkInsertAsync([
            MakeScore(candidateMethodId: 10, memberId: 100, risk: 0.2),
            MakeScore(candidateMethodId: 10, memberId: 101, risk: 0.8),
            MakeScore(candidateMethodId: 10, memberId: 102, risk: 0.5),
            MakeScore(candidateMethodId: 99, memberId: 200, risk: 1.0)  // different candidate
        ]);

        var scores = await repo.GetByCandidateMethodIdAsync(candidateMethodId: 10);

        Assert.Equal(3, scores.Count);
        Assert.All(scores, s => Assert.Equal(10, s.CandidateMethodId));
        Assert.True(scores[0].RiskScore >= scores[1].RiskScore);
        Assert.True(scores[1].RiskScore >= scores[2].RiskScore);
    }

    /// <summary>
    /// GetByMemberIdAsync returns only scores for the given MemberId,
    /// ordered by CreatedAt descending (most recent first).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByMemberIdAsync_ReturnsScoresOrderedByCreatedAtDesc()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CandidateMethodRiskScoreRepository(db);

        var older = MakeScore(candidateMethodId: 20, memberId: 300, risk: 0.4,
            createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = MakeScore(candidateMethodId: 21, memberId: 300, risk: 0.7,
            createdAt: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var other = MakeScore(candidateMethodId: 22, memberId: 999, risk: 0.9);

        await repo.BulkInsertAsync([older, newer, other]);

        var scores = await repo.GetByMemberIdAsync(memberId: 300);

        Assert.Equal(2, scores.Count);
        Assert.All(scores, s => Assert.Equal(300, s.MemberId));
        // Most recent first
        Assert.True(scores[0].CreatedAt >= scores[1].CreatedAt);
    }

    /// <summary>
    /// GetByIdAsync returns null for an ID that does not exist.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CandidateMethodRiskScoreRepository(db);

        var result = await repo.GetByIdAsync(9999);

        Assert.Null(result);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static MethodRiskScore MakeScore(
        int? candidateMethodId,
        int memberId,
        double risk,
        DateTime? createdAt = null) => new()
    {
        CandidateMethodId = candidateMethodId,
        MemberId = memberId,
        RiskScore = risk,
        SelectionReason = $"Risk score {risk:F2}",
        CreatedAt = createdAt ?? DateTime.UtcNow
    };
}
