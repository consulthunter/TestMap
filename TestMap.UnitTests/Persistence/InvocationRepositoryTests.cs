using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Code;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="InvocationRepository"/> using an in-memory SQLite database.
/// Covers the two-phase deduplication in InsertOrUpdateAsync: primary lookup by ContentHash,
/// secondary lookup by (MemberId, InvokedMemberId, FullString, Location) when the hash
/// has changed.
/// </summary>
public sealed class InvocationRepositoryTests
{
    /// <summary>
    /// InsertOrUpdateAsync with a brand-new invocation inserts the row and returns a positive ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_NewInvocation_InsertsAndReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new InvocationRepository(db);

        var id = await repo.InsertOrUpdateAsync(MakeInvocation(memberId: 1, line: 10));

        Assert.True(id > 0);
        Assert.Equal(1, await db.Invocations.CountAsync());
    }

    /// <summary>
    /// Calling InsertOrUpdateAsync twice with identical field values (same ContentHash) returns
    /// the original row's ID without inserting a duplicate row.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_SameContentHash_IsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new InvocationRepository(db);
        var invocation = MakeInvocation(memberId: 2, line: 20);

        var firstId = await repo.InsertOrUpdateAsync(invocation);
        var secondId = await repo.InsertOrUpdateAsync(invocation);

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await db.Invocations.CountAsync());
    }

    /// <summary>
    /// When the ContentHash has changed but MemberId, InvokedMemberId, FullString, and
    /// Location still match, the secondary lookup finds the existing row, updates it in place,
    /// and returns the original ID rather than inserting a new row.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_SameMemberAndLocationButDifferentHash_UpdatesInPlace()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new InvocationRepository(db);

        // First insert
        var original = MakeInvocation(memberId: 3, line: 30, isAssertion: false,
            resolutionStatus: "Resolved");
        var originalId = await repo.InsertOrUpdateAsync(original);

        // Same MemberId / InvokedMemberId / FullString / Location but different resolutionStatus
        // → ContentHash changes (it includes ResolutionStatus in its hash inputs)
        var updated = MakeInvocation(memberId: 3, line: 30, isAssertion: true,
            resolutionStatus: "Unresolved");

        var updatedId = await repo.InsertOrUpdateAsync(updated);

        // Should reuse the existing row
        Assert.Equal(originalId, updatedId);
        Assert.Equal(1, await db.Invocations.CountAsync());

        // The entity fields should reflect the update
        var entity = await db.Invocations.FindAsync(originalId);
        Assert.True(entity!.IsAssertion);
        Assert.Equal("Unresolved", entity.ResolutionStatus);
    }

    /// <summary>
    /// GetByContentHashAsync finds the invocation whose ContentHash matches and returns null
    /// for a hash with no corresponding row.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByContentHashAsync_FindsMatchingInvocation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new InvocationRepository(db);

        var model = MakeInvocation(memberId: 4, line: 40);
        await repo.InsertOrUpdateAsync(model);

        var found = await repo.GetByContentHashAsync(model.ContentHash);
        var notFound = await repo.GetByContentHashAsync("no-such-hash");

        Assert.NotNull(found);
        Assert.Equal(model.ContentHash, found!.ContentHash);
        Assert.Null(notFound);
    }

    /// <summary>
    /// GetAllAsync returns all persisted invocations as domain models.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAllAsync_ReturnsAllInvocations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new InvocationRepository(db);

        await repo.InsertOrUpdateAsync(MakeInvocation(memberId: 1, line: 1));
        await repo.InsertOrUpdateAsync(MakeInvocation(memberId: 2, line: 2));
        await repo.InsertOrUpdateAsync(MakeInvocation(memberId: 3, line: 3));

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
    /// InvocationModel.ContentHash is computed from MemberId, InvokedMemberId, StartLineNumber,
    /// BodyStartPosition, FullString, ResolutionStatus, TargetSymbol, SyntaxKind.
    /// Varying <paramref name="line"/> or <paramref name="resolutionStatus"/> changes the hash.
    /// </summary>
    private static InvocationModel MakeInvocation(
        int memberId,
        int line,
        bool isAssertion = false,
        string resolutionStatus = "Resolved") =>
        new InvocationModel(
            location: new Location(line, 0, line, 10),
            memberId: memberId,
            invokedMemberId: memberId + 100,
            isAssertion: isAssertion,
            fullString: $"obj.Method({line})",
            resolutionStatus: resolutionStatus,
            targetSymbol: "Method",
            syntaxKind: "InvocationExpression",
            callerMemberSymbol: "Caller",
            callerFilePath: "/src/File.cs");
}
