using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Code;

namespace TestMap.Services.Database.Repositories;

public class GeneratedTestRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public GeneratedTestRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }
    
        public async Task InsertGeneratedTest(GenerateTestMethod method)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the import already exists
        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO generated_tests (
                                         test_run_id,
                                         original_method_id,
                                         test_method_id,
                                         filepath,
                                         provider,
                                         model,
                                         strategy,
                                         prompt_token_count,
                                         generation_duration,
                                         generated_body
            ) VALUES (
                      @test_run_id,
                      @original_method_id,
                      @test_method_id,
                      @filepath,
                      @provider,
                      @model,
                      @strategy,
                      @prompt_token_count,
                      @generation_duration,
                      @generated_body
            )
        ";
        
        insertCmd.Parameters.AddWithValue("@test_run_id", method.TestRunId);
        insertCmd.Parameters.AddWithValue("@original_method_id", method.SourceMethodId);
        insertCmd.Parameters.AddWithValue("@test_method_id", method.TestMethodId);
        insertCmd.Parameters.AddWithValue("@filepath", method.FilePath);
        insertCmd.Parameters.AddWithValue("@provider", method.Provider);
        insertCmd.Parameters.AddWithValue("@model", method.Model);
        insertCmd.Parameters.AddWithValue("@strategy", method.Strategy);
        insertCmd.Parameters.AddWithValue("@prompt_token_count", method.TokenCount);
        insertCmd.Parameters.AddWithValue("@generation_duration", method.GenerationDuration);
        insertCmd.Parameters.AddWithValue("@generated_body", method.GeneratedBody);
        
        await insertCmd.ExecuteNonQueryAsync();
    }
}