using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Coverage;

namespace TestMap.Services.Database.Repositories;

public class PackageCoverageRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public PackageCoverageRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task<int> InsertPackageCoverageGetId(PackageCoverage packageCoverage, int reportId, int packageId)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM package_coverage
            WHERE coverage_report_id = @reportId
            AND name = @name;
        ";

        checkCmd.Parameters.AddWithValue("@reportId", reportId);
        checkCmd.Parameters.AddWithValue("@name", packageCoverage.Name);

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
            INSERT INTO package_coverage (
              coverage_report_id, package_id, name, line_rate, branch_rate, complexity
            ) VALUES (
                     @reportId, @packageId, @name, @lineRate, @branchRate, @complexity
            );
        ";

            insertCmd.Parameters.AddWithValue("@reportId", reportId);
            insertCmd.Parameters.AddWithValue("@packageId", packageId);
            insertCmd.Parameters.AddWithValue("@name", packageCoverage.Name);
            insertCmd.Parameters.AddWithValue("@lineRate", packageCoverage.LineRate);
            insertCmd.Parameters.AddWithValue("@branchRate", packageCoverage.BranchRate);
            insertCmd.Parameters.AddWithValue("@complexity", packageCoverage.Complexity);

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            return (int)newId;
        }
    }
}