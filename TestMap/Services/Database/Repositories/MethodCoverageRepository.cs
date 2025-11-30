using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Coverage;

namespace TestMap.Services.Database.Repositories;

public class MethodCoverageRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public MethodCoverageRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task<int> InsertMethodCoverageGetId(MethodCoverage methodCoverage, int classCoverageId, int methodId)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM method_coverage
            WHERE class_coverage_id = @classCoverageId
            AND name = @name;
        ";

        checkCmd.Parameters.AddWithValue("@classCoverageId", classCoverageId);
        checkCmd.Parameters.AddWithValue("@name", methodCoverage.Name);

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
            INSERT INTO method_coverage (
              class_coverage_id, method_id, name, line_rate, branch_rate, complexity
            ) VALUES (
                     @classCoverageId, @methodId, @name, @lineRate, @branchRate, @complexity
            );
        ";
            insertCmd.Parameters.AddWithValue("@classCoverageId", classCoverageId);
            insertCmd.Parameters.AddWithValue("@methodId", methodId);
            insertCmd.Parameters.AddWithValue("@name", methodCoverage.Name);
            insertCmd.Parameters.AddWithValue("@lineRate", methodCoverage.LineRate);
            insertCmd.Parameters.AddWithValue("@branchRate", methodCoverage.BranchRate);
            insertCmd.Parameters.AddWithValue("@complexity", methodCoverage.Complexity);

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            return (int)newId;
        }
    }
}