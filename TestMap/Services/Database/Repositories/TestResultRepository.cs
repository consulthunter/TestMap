using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Results;

namespace TestMap.Services.Database.Repositories;

public class TestResultRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public TestResultRepository(ProjectModel projectModel, string dbPath)
    {
        _projectModel = projectModel;
        _dbPath = dbPath;
    }

    public async Task InsertTestResults(List<TrxTestResult> testResults)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        await using var tx = conn.BeginTransaction();

        foreach (var testResult in testResults)
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = tx;

            insertCmd.CommandText = @"
            INSERT INTO test_results (
              method_id, run_id, run_date, test_name, test_outcome, test_duration, error_message
            ) VALUES (
              @methodId, @runId, @runDate, @testName, @testOutcome, @testDuration, @testErrorMessage
            );";

            insertCmd.Parameters.AddWithValue("@methodId", testResult.MethodId);
            insertCmd.Parameters.AddWithValue("@runId", testResult.RunId);
            insertCmd.Parameters.AddWithValue("@runDate", testResult.RunDate);
            insertCmd.Parameters.AddWithValue("@testName", testResult.TestName ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@testOutcome", testResult.Outcome ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@testDuration", testResult.Duration);
            insertCmd.Parameters.AddWithValue("@testErrorMessage", (object?)testResult.ErrorMessage ?? DBNull.Value);

            await insertCmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }
}