using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Coverage;

namespace TestMap.Services.Database.Repositories;

public class CoverageReportRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public CoverageReportRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task<int> InsertCoverageReportGetId(CoverageReport report, string runId)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM coverage_reports
            WHERE test_run_id = @runId;
        ";

        checkCmd.Parameters.AddWithValue("@runId", runId);

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
            INSERT INTO coverage_reports (
              test_run_id, timestamp, line_rate, branch_rate, lines_covered, lines_valid, branches_valid, branches_covered, complexity
            ) VALUES (
                      @runId, @timestamp, @lineRate, @branchRate, @linesCovered, @linesValid, @branchesValid, @branchesCovered, @complexity
            );
        ";

            insertCmd.Parameters.AddWithValue("@runId", runId);
            insertCmd.Parameters.AddWithValue("@timestamp", report.Timestamp);
            insertCmd.Parameters.AddWithValue("@lineRate", report.LineRate);
            insertCmd.Parameters.AddWithValue("@branchRate", report.BranchRate);
            insertCmd.Parameters.AddWithValue("@linesCovered", report.LinesCovered);
            insertCmd.Parameters.AddWithValue("@linesValid", report.LinesValid);
            insertCmd.Parameters.AddWithValue("@branchesValid", report.BranchesValid);
            insertCmd.Parameters.AddWithValue("@branchesCovered", report.BranchesCovered);
            insertCmd.Parameters.AddWithValue("@complexity", report.Complexity);

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            return (int)newId;
        }
    }
}