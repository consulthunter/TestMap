using Microsoft.Data.Sqlite;
using TestMap.Models;

namespace TestMap.Services.Database.Repositories;

public class ProjectRepository
{
    private readonly ProjectModel _projectModel;
    private readonly string _dbPath;

    public ProjectRepository(ProjectModel projectModel, string dbPath)
    {
        _projectModel = projectModel;
        _dbPath = dbPath;
    }

    public async Task InsertProjectModelGetId()
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the project model
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id FROM projects
            WHERE content_hash = @hash
        ";

        checkCmd.Parameters.AddWithValue("@hash", _projectModel.ContentHash);

        await using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            _projectModel.DbId = reader.GetInt16(0);
        }
        else
        {
            var createdAt = DateTime.UtcNow;
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO projects (
                   owner, repo_name, directory_path, web_url, database_path, last_analyzed_commit, created_at, content_hash
                ) VALUES (
                    @owner, @repoName, @directoryPath, @webUrl, @databasePath, @lastAnalyzedCommit, @createdAt, @contentHash
                );
            ";

            insertCmd.Parameters.AddWithValue("@owner", _projectModel.Owner);
            insertCmd.Parameters.AddWithValue("@repoName", _projectModel.RepoName);
            insertCmd.Parameters.AddWithValue("@directoryPath", _projectModel.DirectoryPath);
            insertCmd.Parameters.AddWithValue("@webUrl", _projectModel.GitHubUrl);
            insertCmd.Parameters.AddWithValue("@databasePath", _dbPath);
            insertCmd.Parameters.AddWithValue("@lastAnalyzedCommit",
                _projectModel.LastAnalyzedCommit ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("@createdAt", createdAt);
            insertCmd.Parameters.AddWithValue("@contentHash", _projectModel.ContentHash);           

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            _projectModel.DbId = (int)newId;
        }
    }
}