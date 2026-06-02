using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="TestMapDatabaseInitializer"/>. Verifies that the initializer
/// drives schema creation via migrations on a blank in-memory database.
/// </summary>
public sealed class TestMapDatabaseInitializerTests
{
    /// <summary>
    /// On a completely blank in-memory database, InitializeAsync runs all pending migrations
    /// and produces a schema that includes the core projects table.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InitializeAsync_OnBlankDatabase_CreatesSchemaViaMigration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);

        var initializer = new TestMapDatabaseInitializer();
        await initializer.InitializeAsync(db);

        Assert.True(await TableExistsAsync(connection, "projects"));
        Assert.True(await TableExistsAsync(connection, "__EFMigrationsHistory"));
        Assert.Empty(await db.Projects.ToListAsync());
    }

    /// <summary>
    /// Calling InitializeAsync twice on the same already-migrated database does not throw.
    /// MigrateAsync is idempotent when all migrations have been applied.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InitializeAsync_CalledTwice_IsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        var initializer = new TestMapDatabaseInitializer();

        await initializer.InitializeAsync(db);
        var ex = await Record.ExceptionAsync(() => initializer.InitializeAsync(db));

        Assert.Null(ex);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static TestMapDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        return new TestMapDbContext(options);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.AddWithValue("$tableName", tableName);
        var result = await command.ExecuteScalarAsync();
        return result != null;
    }
}
