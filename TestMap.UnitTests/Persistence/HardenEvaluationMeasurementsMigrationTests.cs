using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestMap.Persistence.Ef;

namespace TestMap.UnitTests.Persistence;

public sealed class HardenEvaluationMeasurementsMigrationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Migration_BackfillsDistinctRunUidsAndMakesCoverageNullable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260713203258_AddCandidateCohorts");

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO experiment_runs
                (start_time, end_time, project_id, objective, candidate_selection_strategy,
                 configuration, results_file_path, candidate_limit, status,
                 candidate_cohort_id, experiment_series_id)
            VALUES
                ('2026-07-14', NULL, 1, 'test', 'all', '{{}}', 'a.csv', 1, 'Completed', NULL, 'series'),
                ('2026-07-14', NULL, 1, 'test', 'all', '{{}}', 'b.csv', 1, 'Completed', NULL, 'series');
            """);

        await migrator.MigrateAsync();

        await using var uidCommand = connection.CreateCommand();
        uidCommand.CommandText = "SELECT COUNT(*), COUNT(DISTINCT run_uid), MIN(length(run_uid)) FROM experiment_runs;";
        await using var reader = await uidCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(2L, reader.GetInt64(1));
        Assert.Equal(32L, reader.GetInt64(2));

        await reader.DisposeAsync();
        await using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = "SELECT [notnull] FROM pragma_table_info('generated_test_executions') WHERE name = 'coverage_delta';";
        Assert.Equal(0L, (long)(await columnCommand.ExecuteScalarAsync())!);
    }
}
