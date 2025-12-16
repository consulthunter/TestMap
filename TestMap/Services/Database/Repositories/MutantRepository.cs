using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Results;

namespace TestMap.Services.Database.Repositories;

public class MutantRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;
    
    public MutantRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }
    
    public async Task<int> InsertMutant(int fileMutationResultId, int methodId, StrykerMutant mutant)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
        INSERT INTO mutants (
              file_mutation_result_id, method_id, mutant_id, mutator_name, replacement,
              start_line, start_column, end_line, end_column, status,
              status_reason, static_status
        ) VALUES (
             @fileMutationResultId, @methodId, @mutantId, @mutatorName, @replacement,
                  @startLine,  @startColumn,  @endLine,  @endColumn,  @status,
                  @statusReason, @staticStatus
        );
    ";
        insertCmd.Parameters.AddWithValue("@fileMutationResultId", fileMutationResultId);
        insertCmd.Parameters.AddWithValue("@methodId", methodId);
        insertCmd.Parameters.AddWithValue("@mutantId", mutant.id);
        insertCmd.Parameters.AddWithValue("@mutatorName", mutant.mutatorName);
        insertCmd.Parameters.AddWithValue("@replacement", mutant.replacement);
        insertCmd.Parameters.AddWithValue("@startLine", mutant.location.start.line);
        insertCmd.Parameters.AddWithValue("@startColumn", mutant.location.start.column);
        insertCmd.Parameters.AddWithValue("@endLine", mutant.location.end.line);
        insertCmd.Parameters.AddWithValue("@endColumn", mutant.location.end.column);
        insertCmd.Parameters.AddWithValue("@status", mutant.status);
        insertCmd.Parameters.AddWithValue("@statusReason", mutant.statusReason ?? string.Empty);
        insertCmd.Parameters.AddWithValue("@staticStatus", mutant.@static);

        await insertCmd.ExecuteNonQueryAsync();

        var lastIdCmd = conn.CreateCommand();
        lastIdCmd.CommandText = "SELECT last_insert_rowid();";
        var newId = (long)await lastIdCmd.ExecuteScalarAsync();

        return (int)newId;
    }
}