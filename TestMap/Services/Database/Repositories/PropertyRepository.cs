using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Code;

namespace TestMap.Services.Database.Repositories;

public class PropertyRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public PropertyRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task InsertPropertyGetId(PropertyModel property)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM properties
            WHERE content_hash = @hash
        ";
        
        checkCmd.Parameters.AddWithValue("@hash", property.ContentHash);

        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            var guid = reader.GetString(1);

            property.Id = id;
            property.Guid = guid;
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO properties (
              class_id, guid, name, visibility, modifiers, attributes, full_string, 
                                    location_start_lin_no, location_body_start, location_body_end, location_end_lin_no, content_hash
            ) VALUES (
                      @classId, @guid, @name, @visibility, @modifiers, @attributes, @fullString, 
                      @locationStartLinNo, @locationBodyStart, @locationBodyEnd, @locationEndLinNo, @contentHash
            );
        ";

            insertCmd.Parameters.AddWithValue("@classId", property.ClassId);
            insertCmd.Parameters.AddWithValue("@guid", property.Guid);
            insertCmd.Parameters.AddWithValue("@name", property.Name);
            insertCmd.Parameters.AddWithValue("@visibility", property.Visibility);
            insertCmd.Parameters.AddWithValue("@modifiers", property.Modifiers);
            insertCmd.Parameters.AddWithValue("@attributes", property.Attributes);
            insertCmd.Parameters.AddWithValue("@fullString", property.FullString);
            insertCmd.Parameters.AddWithValue("@locationStartLinNo", property.Location.StartLineNumber);
            insertCmd.Parameters.AddWithValue("@locationBodyStart", property.Location.BodyStartPosition);
            insertCmd.Parameters.AddWithValue("@locationBodyEnd", property.Location.BodyEndPosition);
            insertCmd.Parameters.AddWithValue("@locationEndLinNo", property.Location.EndLineNumber);
            insertCmd.Parameters.AddWithValue("@contentHash", property.ContentHash);           

            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            property.Id = (int)newId;
        }
    }
}