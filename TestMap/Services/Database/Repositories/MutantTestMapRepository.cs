using Microsoft.Data.Sqlite;
using TestMap.Models;

namespace TestMap.Services.Database.Repositories;

public class MutantTestMapRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;
    
    public MutantTestMapRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }
    
    public async Task<int> InsertMutantTestMap(int mutantId, int testMethodId, string interactionType)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
        INSERT INTO mutant_test_map (
              mutant_id, test_method_id, interaction_type
        ) VALUES (
             @mutantId, @testMethodId, @interactionType
        );
    ";
        insertCmd.Parameters.AddWithValue("@mutantId", mutantId);
        insertCmd.Parameters.AddWithValue("@testMethodId", testMethodId);
        insertCmd.Parameters.AddWithValue("@interactionType", interactionType);

        await insertCmd.ExecuteNonQueryAsync();

        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)await lastIdCmd.ExecuteScalarAsync();

        return (int)newId;
    }
}