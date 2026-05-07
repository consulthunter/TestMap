using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;

namespace TestMap.UnitTests.Persistence;

public sealed class TestMapDatabaseInitializerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InitializeAsync_CreatesSchemaWhenInitialMigrationCreatedOnlyHistoryTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var db = CreateDbContext(connection);
        await db.Database.MigrateAsync();
        Assert.False(await TableExistsAsync(connection, "projects"));

        var initializer = new TestMapDatabaseInitializer(new SqliteSchemaCompatibilityService());
        await initializer.InitializeAsync(db);

        Assert.True(await TableExistsAsync(connection, "projects"));
        Assert.True(await TableExistsAsync(connection, "__EFMigrationsHistory"));
        Assert.Empty(await db.Projects.ToListAsync());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InitializeAsync_ThrowsClearErrorWhenApplicationTablesExistButProjectsIsMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var db = CreateDbContext(connection);
        await ExecuteNonQueryAsync(connection, "CREATE TABLE legacy_table (id INTEGER NOT NULL);");

        var initializer = new TestMapDatabaseInitializer(new SqliteSchemaCompatibilityService());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync(db));

        Assert.Contains("missing required table 'projects'", ex.Message);
        Assert.Contains("legacy_table", ex.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InitializeAsync_RepairsOrphanCompatibilityTablesWhenCoreSchemaIsMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var db = CreateDbContext(connection);
        await ExecuteNonQueryAsync(connection, "CREATE TABLE rule_definitions (id INTEGER NOT NULL);");
        await ExecuteNonQueryAsync(connection, "CREATE TABLE coverage_gaps (id INTEGER NOT NULL);");

        var initializer = new TestMapDatabaseInitializer(new SqliteSchemaCompatibilityService());
        await initializer.InitializeAsync(db);

        Assert.True(await TableExistsAsync(connection, "projects"));
        Assert.True(await TableExistsAsync(connection, "rule_definitions"));
        Assert.True(await TableExistsAsync(connection, "coverage_gaps"));
        Assert.Empty(await db.Projects.ToListAsync());
    }

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
        command.Parameters.Add(new SqliteParameter("$tableName", tableName));
        var result = await command.ExecuteScalarAsync();
        return result != null;
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
