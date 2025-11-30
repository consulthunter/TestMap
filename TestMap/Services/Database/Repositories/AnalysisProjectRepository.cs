using Microsoft.Data.Sqlite;
using TestMap.Models;

namespace TestMap.Services.Database.Repositories;

public class AnalysisProjectRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public AnalysisProjectRepository(ProjectModel projectModel, string dbPath)
    {
        _projectModel = projectModel;
        _dbPath = dbPath;
    }

    public async Task InsertAnalysisProjectGetId()
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the project exists

        foreach (var project in _projectModel.Projects)
        {
            // Check if already exists
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = @"
                SELECT id, solution_id, guid
                FROM analysis_projects
                WHERE content_hash = @hash;
            ";
            checkCmd.Parameters.AddWithValue("@hash", project.ContentHash);

            await using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int id = reader.GetInt16(0);
                int solutionId = reader.GetInt16(1);
                var guid = reader.GetString(2);

                project.Id = id;
                project.SolutionId = solutionId;
                project.Guid = guid;
            }
            else
            {
                var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO analysis_projects (
                       solution_id, guid, project_path, target_framework, content_hash
                    ) VALUES (
                        @solution_id, @guid, @project_path, @target_framework, @content_hash
                    );
                ";

                insertCmd.Parameters.AddWithValue("@solution_id", project.SolutionId);
                insertCmd.Parameters.AddWithValue("@guid", project.Guid);
                insertCmd.Parameters.AddWithValue("@project_path", project.ProjectFilePath);
                insertCmd.Parameters.AddWithValue("@target_framework", project.LanguageFramework);
                insertCmd.Parameters.AddWithValue("@content_hash", project.ContentHash);

                await insertCmd.ExecuteNonQueryAsync();

                var lastIdCmd = conn.CreateCommand();
                lastIdCmd.CommandText = "SELECT last_insert_rowid();";
                var newId = (long)await lastIdCmd.ExecuteScalarAsync();
                project.Id = (int)newId;
            }
        }
    }
}