using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Code;

namespace TestMap.Services.Database.Repositories;

public class MethodRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public MethodRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task InsertMethodsGetId(MethodModel method)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the method already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM methods
            WHERE content_hash = @hash
        ";
        
        checkCmd.Parameters.AddWithValue("@hash", method.ContentHash);

        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            var guid = reader.GetString(1);

            method.Id = id;
            method.Guid = guid;
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO methods (
              class_id, guid, name, visibility, modifiers, attributes, full_string, doc_string, is_test_method,
                                 testing_framework, test_type, location_start_lin_no, location_body_start, location_body_end,
                                 location_end_lin_no, content_hash
            ) VALUES (
                      @classId, @guid, @name, @visibility, @modifiers, @attributes, @fullString, @docString, @isTestMethod,
                      @testingFramework, @testType, @locationStartLinNo, @locationBodyStart, @locationBodyEnd, @locationEndLinNo, @contentHash
            );
        ";

            insertCmd.Parameters.AddWithValue("@classId", method.ClassId);
            insertCmd.Parameters.AddWithValue("@guid", method.Guid);
            insertCmd.Parameters.AddWithValue("@name", method.Name);
            insertCmd.Parameters.AddWithValue("@visibility", method.Visibility);
            insertCmd.Parameters.AddWithValue("@modifiers", method.Modifiers);
            insertCmd.Parameters.AddWithValue("@attributes", method.Attributes);
            insertCmd.Parameters.AddWithValue("@fullString", method.FullString);
            insertCmd.Parameters.AddWithValue("@docString", method.DocString);
            insertCmd.Parameters.AddWithValue("@isTestMethod", method.IsTestMethod);
            insertCmd.Parameters.AddWithValue("@testingFramework", method.TestingFramework);
            insertCmd.Parameters.AddWithValue("@testType", method.TestType);
            insertCmd.Parameters.AddWithValue("@locationStartLinNo", method.Location.StartLineNumber);
            insertCmd.Parameters.AddWithValue("@locationBodyStart", method.Location.BodyStartPosition);
            insertCmd.Parameters.AddWithValue("@locationBodyEnd", method.Location.BodyEndPosition);
            insertCmd.Parameters.AddWithValue("@locationEndLinNo", method.Location.EndLineNumber);
            insertCmd.Parameters.AddWithValue("@contentHash", method.ContentHash);


            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            method.Id = (int)newId;
        }
    }

    public async Task<int> FindMethod(string name, string filepath)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT m.id
        FROM methods AS m
        JOIN classes AS c ON m.class_id = c.id
        JOIN source_files AS sf ON c.file_id = sf.id
        WHERE m.name = @name AND sf.path = @filepath;
    ";

        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@filepath", filepath);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return reader.GetInt32(0); // m.guid

        return 0; // not found
    }

    public async Task<int> FindMethodFromContains(string name)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT m.id
        FROM methods AS m
        WHERE m.is_test_method = 1
         AND @name LIKE '%' || m.name || '%' COLLATE NOCASE
        LIMIT 1;
    ";

        cmd.Parameters.AddWithValue("@name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return reader.GetInt32(0); // m.guid

        return 0; // not found
    }

    public async Task<int> FindMethodFromExact(string name)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT m.id
        FROM methods AS m
        WHERE m.name = @name
        LIMIT 1;
    ";

        cmd.Parameters.AddWithValue("@name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return reader.GetInt32(0); // m.guid

        return 0; // not found
    }
}