using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Results;

namespace TestMap.Services.Database.Repositories;

public class LizardFileCodeMetricsRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;
    
    public LizardFileCodeMetricsRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }
    
    public async Task<int> InsertLizardFileCodeMetric(string testRunId, int fileId, LizardFileMetrics metrics)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
        INSERT INTO lizard_file_code_metrics (
              test_run_id, file_id, ncss, ccn, function_count
        ) VALUES (
             @testRunId, @fileId, @ncss, @ccn, @functionCount
        );
    ";

        insertCmd.Parameters.AddWithValue("@testRunId", testRunId);
        insertCmd.Parameters.AddWithValue("@fileId", fileId);
        insertCmd.Parameters.AddWithValue("@ncss", metrics.Ncss);
        insertCmd.Parameters.AddWithValue("@ccn", metrics.Ccn);
        insertCmd.Parameters.AddWithValue("@functionCount", metrics.Functions);

        await insertCmd.ExecuteNonQueryAsync();

        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)await lastIdCmd.ExecuteScalarAsync();

        return (int)newId;
    }

    public async Task<bool> HasLizardFileCodeMetrics()
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM lizard_file_code_metrics;
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