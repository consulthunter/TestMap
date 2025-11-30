using Microsoft.Data.Sqlite;
using TestMap.Models;

namespace TestMap.Services.Database.Repositories;

public class AnalysisSolutionRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public AnalysisSolutionRepository(ProjectModel projectModel, string dbPath)
    {
        _projectModel = projectModel;
        _dbPath = dbPath;
    }

    public async Task InsertAnalysisSolutionGetId()
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the solution exists

        foreach (var solution in _projectModel.Solutions)
        {
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = @"
                SELECT id, project_id, guid  FROM analysis_solutions
                WHERE content_hash = @hash;
            ";

            checkCmd.Parameters.AddWithValue("@hash", solution.ContentHash);

            using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int id = reader.GetInt16(0);
                int projectId = reader.GetInt16(1);
                var guid = reader.GetString(2);

                solution.Id = id;
                solution.ProjectModelId = projectId;
                solution.Guid = guid;
            }
            else
            {
                var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO analysis_solutions (
                       project_id, solution_path, guid, content_hash
                    ) VALUES (
                        @project_id, @solution_path, @guid, @content_hash
                    );
                ";

                insertCmd.Parameters.AddWithValue("@project_id", _projectModel.DbId);
                insertCmd.Parameters.AddWithValue("@solution_path", solution.SolutionFilePath);
                insertCmd.Parameters.AddWithValue("@guid", solution.Guid);
                insertCmd.Parameters.AddWithValue("@content_hash", solution.ContentHash);

                await insertCmd.ExecuteNonQueryAsync();

                var lastIdCmd = conn.CreateCommand();
                lastIdCmd.CommandText = "SELECT last_insert_rowid();";
                var newId = (long)await lastIdCmd.ExecuteScalarAsync();
                solution.Id = (int)newId;
            }
        }
    }
}