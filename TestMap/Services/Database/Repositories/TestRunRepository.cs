using Microsoft.Data.Sqlite;
using TestMap.Models;

namespace TestMap.Services.Database.Repositories;

public class TestRunRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public TestRunRepository(ProjectModel projectModel, string dbPath)
    {
        _projectModel = projectModel;
        _dbPath = dbPath;
    }

    public async Task<int> InsertTestRun(string runId, string runDate, string result, int coverage, string? logPath,
        string? error)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
        INSERT INTO test_runs (
            run_id,
            run_date,
            result,
            coverage,
            log_path,
            error
        ) VALUES (
            @runId,
            @runDate,
            @result,
            @coverage,
            @logPath,
            @error
        );
    ";

        insertCmd.Parameters.AddWithValue("@runId", runId);
        insertCmd.Parameters.AddWithValue("@runDate", runDate);
        insertCmd.Parameters.AddWithValue("@result", result);
        insertCmd.Parameters.AddWithValue("@coverage", coverage);
        insertCmd.Parameters.AddWithValue("@logPath", logPath ?? (object)DBNull.Value);
        insertCmd.Parameters.AddWithValue("@error", error ?? (object)DBNull.Value);

        await insertCmd.ExecuteNonQueryAsync();

        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)await lastIdCmd.ExecuteScalarAsync();

        return (int)newId;
    }

    public async Task UpdateTestRunStatus(
        string runId,
        string result,
        int coverage,
        string? logPath,
        string? error)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        UPDATE test_runs
        SET result = @result,
            coverage = @coverage,
            log_path = @logPath,
            error = @error
        WHERE run_id = @runId;
    ";

        cmd.Parameters.AddWithValue("@result", result);
        cmd.Parameters.AddWithValue("@coverage", coverage);
        cmd.Parameters.AddWithValue("@logPath", logPath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@error", error ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@runId", runId);

        await cmd.ExecuteNonQueryAsync();
    }
}