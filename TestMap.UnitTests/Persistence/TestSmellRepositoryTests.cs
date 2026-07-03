using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Testing;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Testing;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="TestSmellRepository"/> using an in-memory SQLite database.
/// Covers the 7-field composite key used for upsert deduplication, the HasChanged
/// guard that avoids unnecessary writes, member-scoped queries, and idempotency.
/// </summary>
public sealed class TestSmellRepositoryTests
{
    /// <summary>
    /// InsertOrUpdateAsync with a new unique key combination inserts the row and
    /// returns a positive ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_NewSmell_InsertsAndReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestSmellRepository(db);

        var id = await repo.InsertOrUpdateAsync(MakeSmell(projectId: 1, smellId: "SM001", line: 10));

        Assert.True(id > 0);
        Assert.Equal(1, await db.TestSmells.CountAsync());
    }

    /// <summary>
    /// Calling InsertOrUpdateAsync twice with the same 7-field composite key does not
    /// create a duplicate row — it returns the existing row's ID (idempotent).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_SameCompositeKey_IsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestSmellRepository(db);
        var smell = MakeSmell(projectId: 2, smellId: "SM002", line: 20);

        var firstId  = await repo.InsertOrUpdateAsync(smell);
        var secondId = await repo.InsertOrUpdateAsync(smell);

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await db.TestSmells.CountAsync());
    }

    /// <summary>
    /// When the same composite key exists but a non-key field (Message) has changed,
    /// InsertOrUpdateAsync updates that field in place.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_ChangedMessage_UpdatesInPlace()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestSmellRepository(db);

        var firstId = await repo.InsertOrUpdateAsync(
            MakeSmell(projectId: 3, smellId: "SM003", line: 30, message: "Original"));

        await repo.InsertOrUpdateAsync(
            MakeSmell(projectId: 3, smellId: "SM003", line: 30, message: "Updated"));

        var entity = await db.TestSmells.FindAsync(firstId);
        Assert.Equal("Updated", entity!.Message);
        Assert.Equal(1, await db.TestSmells.CountAsync());
    }

    /// <summary>
    /// A different line number is part of the composite key, so two smells with
    /// otherwise identical fields but different Line are stored as separate rows.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_DifferentLine_InsertsNewRow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestSmellRepository(db);

        await repo.InsertOrUpdateAsync(MakeSmell(projectId: 4, smellId: "SM004", line: 40));
        await repo.InsertOrUpdateAsync(MakeSmell(projectId: 4, smellId: "SM004", line: 41));

        Assert.Equal(2, await db.TestSmells.CountAsync());
    }

    /// <summary>
    /// GetByMemberIdAsync returns only smells associated with the specified MemberId.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByMemberIdAsync_ReturnsSmellsForMember()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestSmellRepository(db);

        await repo.InsertOrUpdateAsync(MakeSmell(projectId: 5, smellId: "A", line: 1, memberId: 10));
        await repo.InsertOrUpdateAsync(MakeSmell(projectId: 5, smellId: "B", line: 2, memberId: 10));
        await repo.InsertOrUpdateAsync(MakeSmell(projectId: 5, smellId: "C", line: 3, memberId: 99));

        var memberSmells = await repo.GetByMemberIdAsync(memberId: 10);

        Assert.Equal(2, memberSmells.Count);
        Assert.All(memberSmells, s => Assert.Equal(10, s.MemberId));
    }

    /// <summary>
    /// GetAllAsync returns all persisted smells across all projects and members.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_ReturnsAllSmells()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new TestSmellRepository(db);

        await repo.InsertOrUpdateAsync(MakeSmell(projectId: 6, smellId: "X", line: 100));
        await repo.InsertOrUpdateAsync(MakeSmell(projectId: 7, smellId: "Y", line: 200));
        await repo.InsertOrUpdateAsync(MakeSmell(projectId: 8, smellId: "Z", line: 300));

        var all = await repo.GetAllAsync();

        Assert.Equal(3, all.Count);
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
    /// The composite key in InsertOrUpdateAsync is:
    /// (ProjectId, MemberId, ObjectId, SmellId, FilePath, Line, Column).
    /// </summary>
    private static TestSmellModel MakeSmell(
        int projectId,
        string smellId,
        int line,
        int? memberId = null,
        string message = "Test smell detected") => new()
    {
        ProjectId = projectId,
        MemberId = memberId,
        ObjectId = null,
        SmellId = smellId,
        SmellName = $"Smell_{smellId}",
        Message = message,
        FilePath = $"/tests/Test{projectId}.cs",
        Line = line,
        Column = 1,
        ContainingTypeName = $"TestClass{projectId}",
        TestMethodName = $"Test_Method_{line}",
        AnalyzedAtUtc = DateTimeOffset.UtcNow
    };
}
