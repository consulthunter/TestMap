using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace TestMap.Persistence.Ef;

public sealed class TestMapDatabaseInitializer
{
    private const string ProjectsTableName = "projects";
    private static readonly HashSet<string> CompatibilityOnlyTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "coverage_gaps",
        "mutation_testing_reports",
        "mutants",
        "mutant_survived_tests",
        "candidate_method_risk_scores",
        "candidate_inventory",
        "experiment_matrix_work_items",
        "test_execution_results",
        "flaky_test_scores",
        "flaky_test_rerun_results",
        "rule_definitions",
        "rule_decisions"
    };

    private readonly SqliteSchemaCompatibilityService _schemaCompatibility;

    public TestMapDatabaseInitializer(SqliteSchemaCompatibilityService schemaCompatibility)
    {
        _schemaCompatibility = schemaCompatibility;
    }

    public async Task InitializeAsync(TestMapDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(db, ProjectsTableName, cancellationToken))
            await RepairEmptyMigrationOnlyDatabaseAsync(db, cancellationToken);

        await db.Database.MigrateAsync(cancellationToken);

        if (!await TableExistsAsync(db, ProjectsTableName, cancellationToken))
            await RepairEmptyMigrationOnlyDatabaseAsync(db, cancellationToken);

        await _schemaCompatibility.EnsureCompatibleAsync(db);
    }

    private static async Task RepairEmptyMigrationOnlyDatabaseAsync(
        TestMapDbContext db,
        CancellationToken cancellationToken)
    {
        var applicationTables = await GetApplicationTablesAsync(db, cancellationToken);
        if (applicationTables.Any(x => !CompatibilityOnlyTables.Contains(x)))
            throw new InvalidOperationException(
                $"The SQLite database is missing required table '{ProjectsTableName}' but contains application tables: {string.Join(", ", applicationTables)}. The database schema is inconsistent and cannot be initialized automatically.");

        foreach (var table in applicationTables)
            await ExecuteNonQueryAsync(db, $"DROP TABLE IF EXISTS \"{table}\";", cancellationToken);

        await ExecuteNonQueryAsync(db, "DROP TABLE IF EXISTS __EFMigrationsHistory;", cancellationToken);
        await ExecuteNonQueryAsync(db, "DROP TABLE IF EXISTS __EFMigrationsLock;", cancellationToken);
        var databaseCreator = db.Database.GetService<IRelationalDatabaseCreator>();
        await databaseCreator.CreateTablesAsync(cancellationToken);
        await MarkKnownMigrationsAppliedAsync(db, cancellationToken);

        if (!await TableExistsAsync(db, ProjectsTableName, cancellationToken))
            throw new InvalidOperationException(
                $"The SQLite database schema was initialized, but required table '{ProjectsTableName}' still does not exist.");
    }

    private static async Task MarkKnownMigrationsAppliedAsync(
        TestMapDbContext db,
        CancellationToken cancellationToken)
    {
        var migrations = db.Database.GetMigrations().ToList();
        if (migrations.Count == 0) return;

        await ExecuteNonQueryAsync(
            db,
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """,
            cancellationToken);

        var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? string.Empty;
        foreach (var migration in migrations)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                                  INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                                  VALUES ($migrationId, $productVersion);
                                  """;
            command.Parameters.Add(new SqliteParameter("$migrationId", migration));
            command.Parameters.Add(new SqliteParameter("$productVersion", productVersion));

            if (command.Connection!.State != System.Data.ConnectionState.Open)
                await command.Connection.OpenAsync(cancellationToken);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(
        TestMapDbContext db,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.Add(new SqliteParameter("$tableName", tableName));

        if (command.Connection!.State != System.Data.ConnectionState.Open)
            await command.Connection.OpenAsync(cancellationToken);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    private static async Task<IReadOnlyList<string>> GetApplicationTablesAsync(
        TestMapDbContext db,
        CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
                              SELECT name
                              FROM sqlite_master
                              WHERE type = 'table'
                                AND name <> '__EFMigrationsHistory'
                                AND name <> '__EFMigrationsLock'
                                AND name NOT LIKE 'sqlite_%'
                              ORDER BY name;
                              """;

        if (command.Connection!.State != System.Data.ConnectionState.Open)
            await command.Connection.OpenAsync(cancellationToken);

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tables.Add(reader.GetString(0));

        return tables;
    }

    private static async Task ExecuteNonQueryAsync(
        TestMapDbContext db,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        if (command.Connection!.State != System.Data.ConnectionState.Open)
            await command.Connection.OpenAsync(cancellationToken);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
