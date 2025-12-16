using Microsoft.Data.Sqlite;
using TestMap.Models;

namespace TestMap.Services.Database.Repositories;

public class MutationReportRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;
    
    public MutationReportRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }
    
    public async Task<int> InsertMutationReport(string runId, string runDate, string projectRoot, string schemaVersion, string fullReportJson)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
        INSERT INTO mutation_reports (
              test_run_id,
              timestamp,
              project_root,
              schema_version,
              full_report_json
        ) VALUES (
                  @runId, @runDate, @projectRoot, @schemaVersion, @fullReportJson
        );
    ";
        insertCmd.Parameters.AddWithValue("@runId", runId);
        insertCmd.Parameters.AddWithValue("@runDate", runDate);
        insertCmd.Parameters.AddWithValue("@projectRoot", projectRoot);
        insertCmd.Parameters.AddWithValue("@schemaVersion", schemaVersion);
        insertCmd.Parameters.AddWithValue("@fullReportJson", fullReportJson);

        await insertCmd.ExecuteNonQueryAsync();

        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)await lastIdCmd.ExecuteScalarAsync();

        return (int)newId;
    }

    public async Task<bool> HasMutationReports()
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM mutation_reports;
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