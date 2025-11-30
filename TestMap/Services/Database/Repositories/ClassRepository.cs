using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Code;

namespace TestMap.Services.Database.Repositories;

public class ClassRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public ClassRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task InsertClassesGetId(ClassModel cla)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the class already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM classes
            WHERE content_hash = @hash
        ";
        
        checkCmd.Parameters.AddWithValue("@hash", cla.ContentHash);

        await using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            var guid = reader.GetString(1);

            cla.Id = id;
            cla.Guid = guid;
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO classes (
                file_id, guid, name, visibility, modifiers, attributes, full_string, doc_string, is_test_class,
                                 testing_framework, location_start_lin_no, location_body_start, location_body_end,
                                 location_end_lin_no, content_hash
            ) VALUES (
                @fileId, @guid, @name, @visibility, @modifiers, @attributes, @fullString, @docString, @isTestClass,
                      @testingFramework, @locationStartLinNo, @locationBodyStart, @locationBodyEnd, @locationEndLinNo, @contentHash
            );
        ";

            insertCmd.Parameters.AddWithValue("@fileId", cla.FileId);
            insertCmd.Parameters.AddWithValue("@guid", cla.Guid);
            insertCmd.Parameters.AddWithValue("@name", cla.Name);
            insertCmd.Parameters.AddWithValue("@visibility", cla.Visibility);
            insertCmd.Parameters.AddWithValue("@modifiers", cla.Modifiers);
            insertCmd.Parameters.AddWithValue("@attributes", cla.Attributes);
            insertCmd.Parameters.AddWithValue("@fullString", cla.FullString);
            insertCmd.Parameters.AddWithValue("@docString", cla.DocString);
            insertCmd.Parameters.AddWithValue("@isTestClass", cla.IsTestClass);
            insertCmd.Parameters.AddWithValue("@testingFramework", cla.TestingFramework);
            insertCmd.Parameters.AddWithValue("@locationStartLinNo", cla.Location.StartLineNumber);
            insertCmd.Parameters.AddWithValue("@locationBodyStart", cla.Location.BodyStartPosition);
            insertCmd.Parameters.AddWithValue("@locationBodyEnd", cla.Location.BodyEndPosition);
            insertCmd.Parameters.AddWithValue("@locationEndLinNo", cla.Location.EndLineNumber);
            insertCmd.Parameters.AddWithValue("@contentHash", cla.ContentHash);

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            cla.Id = (int)newId;
        }
    }

    public async Task<int> FindClass(string name)
    {
        // Extract simple class name
        var className = name.Split('.').Last();

        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT c.id
        FROM classes AS c
        WHERE c.name = @name
        LIMIT 1;
    ";
        cmd.Parameters.AddWithValue("@name", className);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return reader.GetInt32(0); // m.guid

        return 0; // not found
    }
}