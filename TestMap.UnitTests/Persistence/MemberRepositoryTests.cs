using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Code;

namespace TestMap.UnitTests.Persistence;

public sealed class MemberRepositoryTests
{
    [Fact]
    public async Task InsertOrUpdateAsync_BaselineBodyChange_UpdatesStableMemberInPlace()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repository = new MemberRepository(db);

        var originalId = await repository.InsertOrUpdateAsync(Member("return 1;"));
        var updatedId = await repository.InsertOrUpdateAsync(Member("return 2;"));

        Assert.Equal(originalId, updatedId);
        Assert.Equal(1, await db.Members.CountAsync());
        Assert.Contains("return 2", (await db.Members.FindAsync(originalId))!.FullString);
    }

    [Fact]
    public async Task InsertOrUpdateAsync_AttemptAnalysis_DoesNotRewriteBaseline()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repository = new MemberRepository(db);

        var baselineId = await repository.InsertOrUpdateAsync(Member("return 1;"));
        var unchangedId = await repository.InsertOrUpdateAsync(
            Member("return 1;", MemberOrigin.ToolAttempt, 42));
        var changedId = await repository.InsertOrUpdateAsync(
            Member("return 2;", MemberOrigin.ToolAttempt, 42));

        Assert.Equal(baselineId, unchangedId);
        Assert.NotEqual(baselineId, changedId);
        Assert.Equal("return 1;", (await db.Members.FindAsync(baselineId))!.FullString);
        var changed = await db.Members.FindAsync(changedId);
        Assert.Equal(MemberOrigin.ToolAttempt, changed!.OriginKind);
        Assert.Equal(42, changed.OriginAttemptId);
    }

    private static MemberModel Member(
        string body,
        string originKind = MemberOrigin.Baseline,
        int? originAttemptId = null) =>
        new(
            [],
            [],
            [],
            new Location(0, 0, 0, 0),
            objectEntityId: 7,
            name: "Calculate",
            kind: "method",
            fullString: body,
            signature: "global::Example.Calculate(int)",
            originKind: originKind,
            originAttemptId: originAttemptId);

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
