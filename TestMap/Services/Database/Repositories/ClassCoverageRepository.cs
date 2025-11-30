using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Coverage;

namespace TestMap.Services.Database.Repositories;

public class ClassCoverageRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public ClassCoverageRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task<int> InsertClassCoverageGetId(ClassCoverage classCoverage, int packageCovId, int classId)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM class_coverage
            WHERE package_coverage_id = @packageCovId
            AND name = @name;
        ";

        checkCmd.Parameters.AddWithValue("@packageCovId", packageCovId);
        checkCmd.Parameters.AddWithValue("@name", classCoverage.Name);

        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);

            return id;
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO class_coverage (
              package_coverage_id, class_id, name, line_rate, branch_rate, complexity
            ) VALUES (
                     @packageCovId, @classId, @name, @lineRate, @branchRate, @complexity
            );
        ";

            insertCmd.Parameters.AddWithValue("@packageCovId", packageCovId);
            insertCmd.Parameters.AddWithValue("@classId", classId);
            insertCmd.Parameters.AddWithValue("@name", classCoverage.Name);
            insertCmd.Parameters.AddWithValue("@lineRate", classCoverage.LineRate);
            insertCmd.Parameters.AddWithValue("@branchRate", classCoverage.BranchRate);
            insertCmd.Parameters.AddWithValue("@complexity", classCoverage.Complexity);

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            return (int)newId;
        }
    }
}