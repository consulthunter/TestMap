using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;

namespace TestMap.IntegrationTests.Persistence;

/// <summary>
/// Integration tests that verify the EF Core migration chain against a real file-based
/// SQLite database. Each test creates a unique temp file and deletes it on completion.
/// These tests are the acceptance criteria for "migrations as source of truth":
/// <c>MigrateAsync</c> on a blank database must produce a correct, complete schema.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Execution", "LocalOnly")]
public sealed class MigrationSchemaTests
{
    /// <summary>
    /// Running MigrateAsync on a blank SQLite file creates all expected application tables.
    /// This is the primary acceptance test for the migration reset — it would have failed
    /// before InitialCreate.Up was properly populated.
    /// </summary>
    [Fact]
    public async Task MigrateAsync_OnBlankDatabase_CreatesAllExpectedTables()
    {
        await WithTempDatabaseAsync(async (db, _) =>
        {
            await db.Database.MigrateAsync();

            var tables = await GetTableNamesAsync(db);

            // Core project and code tables
            Assert.Contains("projects", tables);
            Assert.Contains("csharp_solutions", tables);
            Assert.Contains("csharp_projects", tables);
            Assert.Contains("members", tables);
            Assert.Contains("invocations", tables);

            // Testing
            Assert.Contains("test_runs", tables);
            Assert.Contains("test_results", tables);

            // Coverage and mutation
            Assert.Contains("coverage_reports", tables);
            Assert.Contains("coverage_gaps", tables);
            Assert.Contains("mutation_testing_reports", tables);
            Assert.Contains("mutants", tables);

            // Experiment
            Assert.Contains("experiment_runs", tables);
            Assert.Contains("candidate_inventory", tables);
            Assert.Contains("candidate_methods", tables);
            Assert.Contains("generation_attempts", tables);
            Assert.Contains("generated_test_executions", tables);
            Assert.Contains("tool_attempts", tables);
            Assert.Contains("source_test_mappings", tables);
            Assert.Contains("source_test_mapping_trace_steps", tables);

            // Rules and risk
            Assert.Contains("rule_definitions", tables);
            Assert.Contains("rule_decisions", tables);
        });
    }

    /// <summary>
    /// Running MigrateAsync a second time on an already-migrated database does not throw.
    /// </summary>
    [Fact]
    public async Task MigrateAsync_CalledTwice_IsIdempotent()
    {
        await WithTempDatabaseAsync(async (db, _) =>
        {
            await db.Database.MigrateAsync();
            var ex = await Record.ExceptionAsync(() => db.Database.MigrateAsync());

            Assert.Null(ex);
        });
    }

    /// <summary>
    /// After migration the expected migration IDs are recorded in __EFMigrationsHistory.
    /// </summary>
    [Fact]
    public async Task MigrationHistory_ContainsExpectedMigrations()
    {
        await WithTempDatabaseAsync(async (db, _) =>
        {
            await db.Database.MigrateAsync();

            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();

            Assert.Equal(7, applied.Count);
            Assert.Contains(applied, x => x.Contains("InitialCreate"));
            Assert.Contains(applied, x => x.Contains("AddToolAttempts"));
            Assert.Contains(applied, x => x.Contains("AddToolAttemptPostMeasurement"));
            Assert.Contains(applied, x => x.Contains("AddBasicExtensionPatchMetadata"));
            Assert.Contains(applied, x => x.Contains("AddEvaluationTimingMetrics"));
            Assert.Contains(applied, x => x.Contains("AddToolAttemptLogPaths"));
            Assert.Contains(applied, x => x.Contains("AddGenerationAttemptModifiedFileSnapshot"));
        });
    }

    [Fact]
    public async Task MigrateAsync_AddsEvaluationTimingColumnsToBothAttemptTables()
    {
        await WithTempDatabaseAsync(async (db, _) =>
        {
            await db.Database.MigrateAsync();

            var generationColumns = await GetColumnNamesAsync(db, "generation_attempts");
            var toolColumns = await GetColumnNamesAsync(db, "tool_attempts");

            foreach (var column in new[]
                     {
                         "generation_duration_seconds",
                         "validation_duration_seconds",
                         "total_attempt_duration_seconds"
                     })
            {
                Assert.Contains(column, generationColumns);
                Assert.Contains(column, toolColumns);
            }
        });
    }

    [Fact]
    public async Task MigrateAsync_AddsToolAttemptLogPathColumns()
    {
        await WithTempDatabaseAsync(async (db, _) =>
        {
            await db.Database.MigrateAsync();

            var columns = await GetColumnNamesAsync(db, "tool_attempts");

            Assert.Contains("stdout_log_path", columns);
            Assert.Contains("stderr_log_path", columns);
            Assert.Contains("jsonl_log_path", columns);
        });
    }

    [Fact]
    public async Task MigrateAsync_AddsGenerationAttemptModifiedFileColumns()
    {
        await WithTempDatabaseAsync(async (db, _) =>
        {
            await db.Database.MigrateAsync();

            var columns = await GetColumnNamesAsync(db, "generation_attempts");

            Assert.Contains("modified_file_path", columns);
            Assert.Contains("modified_file_contents", columns);
            Assert.Contains("modified_file_sha256", columns);
        });
    }

    /// <summary>
    /// After migration the database has no pending migrations — the schema is fully up to date.
    /// </summary>
    [Fact]
    public async Task MigrateAsync_LeavesNoPendingMigrations()
    {
        await WithTempDatabaseAsync(async (db, _) =>
        {
            await db.Database.MigrateAsync();

            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

            Assert.Empty(pending);
        });
    }

    /// <summary>
    /// TestMapDatabaseInitializer.InitializeAsync produces the same result as calling
    /// MigrateAsync directly, including the projects table being present.
    /// </summary>
    [Fact]
    public async Task DatabaseInitializer_InitializeAsync_ProducesCompleteSchema()
    {
        await WithTempDatabaseAsync(async (db, _) =>
        {
            var initializer = new TestMapDatabaseInitializer();
            await initializer.InitializeAsync(db);

            var tables = await GetTableNamesAsync(db);

            Assert.Contains("projects", tables);
            Assert.Contains("candidate_inventory", tables);
            Assert.Contains("generation_attempts", tables);
            Assert.Empty(await db.Projects.ToListAsync());
        });
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task WithTempDatabaseAsync(Func<TestMapDbContext, string, Task> test)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"testmap_migration_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<TestMapDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using var db = new TestMapDbContext(options);
            await test(db, dbPath);
        }
        finally
        {
            // Clear the SQLite connection pool so the file handle is released before deletion.
            SqliteConnection.ClearAllPools();

            if (File.Exists(dbPath)) File.Delete(dbPath);
            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";
            if (File.Exists(walPath)) File.Delete(walPath);
            if (File.Exists(shmPath)) File.Delete(shmPath);
        }
    }

    private static async Task<IReadOnlyList<string>> GetTableNamesAsync(TestMapDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
              AND name != '__EFMigrationsHistory'
            ORDER BY name;
            """;

        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    private static async Task<IReadOnlyList<string>> GetColumnNamesAsync(
        TestMapDbContext db,
        string tableName)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\");";

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        return columns;
    }
}
