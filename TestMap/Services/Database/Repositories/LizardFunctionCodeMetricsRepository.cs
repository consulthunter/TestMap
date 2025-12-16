using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Results;

namespace TestMap.Services.Database.Repositories;

public class LizardFunctionCodeMetricsRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;
    
    public LizardFunctionCodeMetricsRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }
    
    public async Task<int> InsertLizardFunctionCodeMetric(string testRunId, int methodId, LizardFunctionMetrics metrics)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
        INSERT INTO lizard_function_code_metrics (
              test_run_id, method_id, ncss, ccn
        ) VALUES (
             @testRunId, @methodId, @ncss, @ccn
        );
    ";

        insertCmd.Parameters.AddWithValue("@testRunId", testRunId);
        insertCmd.Parameters.AddWithValue("@methodId", methodId);
        insertCmd.Parameters.AddWithValue("@ncss", metrics.Ncss);
        insertCmd.Parameters.AddWithValue("@ccn", metrics.Ccn);

        await insertCmd.ExecuteNonQueryAsync();

        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)await lastIdCmd.ExecuteScalarAsync();

        return (int)newId;
    }

    public async Task<bool> HasLizardFunctionCodeMetrics()
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM lizard_function_code_metrics;
        ";
        

        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);

            if (id > 0)
            {
                return true;
            }
        }
        return false;
    }
}