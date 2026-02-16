using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Coverage;

namespace TestMap.Services.Database.Repositories;

public class MethodTestSmellRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public MethodTestSmellRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task<int> InsertMethodTestSmellGetId(int methodId, int testSmellId, string status)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM method_test_smells
            WHERE method_id = @methodId
            AND test_smell_id = @testSmellId;
        ";

        checkCmd.Parameters.AddWithValue("@methodId", methodId);
        checkCmd.Parameters.AddWithValue("@testSmellId", testSmellId);

        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);

            return id;
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO method_test_smells (
              method_id, test_smell_id, status
            ) VALUES (
                     @methodId, @testSmellId, @status
            );
        ";
            
            insertCmd.Parameters.AddWithValue("@methodId", methodId);
            insertCmd.Parameters.AddWithValue("@testSmellId", testSmellId);
            insertCmd.Parameters.AddWithValue("@status", status);

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            return (int)newId;
        }
    }
}