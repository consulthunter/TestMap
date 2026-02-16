using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Code;

namespace TestMap.Services.Database.Repositories;

public class SourceFileRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public SourceFileRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }


    public async Task InsertFileGetId(FileModel file)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the file already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM source_files
            WHERE content_hash = @hash
        ";
        
        checkCmd.Parameters.AddWithValue("@hash", file.ContentHash);

        await using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            var guid = reader.GetString(1);

            file.Id = id;
            file.Guid = guid;
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO source_files (
                analysis_project_id, guid, namespace, name, language, meta_data, usings, path, content_hash
            ) VALUES (
                @analysis_project_id, @guid, @namespace, @name, @language, @meta_data, @usings, @path, @content_hash
            );
        ";

            insertCmd.Parameters.AddWithValue("@analysis_project_id", file.AnalysisProjectId);
            insertCmd.Parameters.AddWithValue("@guid", file.Guid);
            insertCmd.Parameters.AddWithValue("@namespace", file.Namespace);
            insertCmd.Parameters.AddWithValue("@name", file.Name);
            insertCmd.Parameters.AddWithValue("@language", file.Language);
            insertCmd.Parameters.AddWithValue("@meta_data", file.MetaData);
            insertCmd.Parameters.AddWithValue("@usings", file.UsingStatements);
            insertCmd.Parameters.AddWithValue("@path", file.FilePath);
            insertCmd.Parameters.AddWithValue("@content_hash", file.ContentHash);


            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            file.Id = (int)newId;
        }
    }
    
    public async Task<int> FindSourceFile(string name, string filepath)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT sc.id
        FROM source_files AS sc
        WHERE sc.name LIKE '%' || @name || '%' COLLATE NOCASE
        AND sc.path LIKE '%' || @filepath || '%' COLLATE NOCASE
        LIMIT 1;
    ";

        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@filepath", filepath);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return reader.GetInt32(0); // m.guid

        return 0; // not found
    }
}