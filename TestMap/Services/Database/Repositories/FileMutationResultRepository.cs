using Microsoft.Data.Sqlite;
using TestMap.Models;

namespace TestMap.Services.Database.Repositories;

public class FileMutationResultRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public FileMutationResultRepository(ProjectModel projectModel, string dbPath)
    {
        _projectModel = projectModel;
        _dbPath = dbPath;
    }

    public async Task<int> InsertFileMutationResult(int mutationReportId, int sourceFileId, string language,
        double mutationScore)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
        INSERT INTO file_mutation_result (
            mutation_report_id, source_file_id, language, mutation_score
        ) VALUES (
            @mutationReportId, @sourceFileId, @language, @mutationScore
        );
    ";
        
        insertCmd.Parameters.AddWithValue("@mutationReportId", mutationReportId);
        insertCmd.Parameters.AddWithValue("@sourceFileId", sourceFileId);
        insertCmd.Parameters.AddWithValue("@language", language);
        insertCmd.Parameters.AddWithValue("@mutationScore", mutationScore);

        await insertCmd.ExecuteNonQueryAsync();

        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)await lastIdCmd.ExecuteScalarAsync();

        return (int)newId;
    }

}