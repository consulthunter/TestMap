using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Database;

namespace TestMap.Services.Database.Repositories;

public class ImportRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public ImportRepository(ProjectModel projectModel, string dbPath)
    {
        _projectModel = projectModel;
        _dbPath = dbPath;
    }

    public async Task InsertImports(ImportModel import)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the import already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM imports
            WHERE import_name = @importName
              AND import_path = @importPath
              AND is_local = @isLocal;
        ";

        checkCmd.Parameters.AddWithValue("@importName", import.ImportName);
        checkCmd.Parameters.AddWithValue("@importPath", import.ImportPath);
        checkCmd.Parameters.AddWithValue("@isLocal", import.IsLocal);

        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            var guid = reader.GetString(1);

            import.Id = id;
            import.Guid = guid;
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO imports (
                file_id, guid, import_name, import_path, full_string, is_local
            ) VALUES (
                @fileId, @guid, @importName, @importPath, @fullString, @isLocal
            );
        ";

            insertCmd.Parameters.AddWithValue("@fileId", import.FileId);
            insertCmd.Parameters.AddWithValue("@guid", import.Guid);
            insertCmd.Parameters.AddWithValue("@importName", import.ImportName);
            insertCmd.Parameters.AddWithValue("@importPath", import.ImportPath);
            insertCmd.Parameters.AddWithValue("@fullString", import.FullString);
            insertCmd.Parameters.AddWithValue("@isLocal", import.IsLocal);

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            import.Id = (int)newId;
        }
    }
}