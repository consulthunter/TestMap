using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Coverage;

namespace TestMap.Services.Database.Repositories;

public class TestSmellRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public TestSmellRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task<int> FindTestSmell(string name)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT ts.id
        FROM test_smells AS ts
        WHERE ts.name = @name
    ";

        cmd.Parameters.AddWithValue("@name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return reader.GetInt32(0); // m.guid

        return 0; // not found
    }
}