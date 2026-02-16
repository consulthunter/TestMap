using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Database;

namespace TestMap.Services.Database.Repositories;

public class InvocationRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public InvocationRepository(ProjectModel projectModel, string dbPath)
    {
        _projectModel = projectModel;
        _dbPath = dbPath;
    }

    public async Task InsertInvocationsGetId(InvocationModel invocation)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the invocation already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM invocations
            WHERE content_hash = @hash
        ";
        
        checkCmd.Parameters.AddWithValue("@hash", invocation.ContentHash);

        await using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            var guid = reader.GetString(1);

            invocation.Id = id;
            invocation.Guid = guid;
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO invocations (
              target_method_id, source_method_id, guid, is_assertion, full_string, 
                                     location_start_lin_no, location_body_start, location_body_end, location_end_lin_no, content_hash
            ) VALUES (
                      @targetMethodId, @sourceMethodId, @guid, @isAssertion, @fullString, 
                      @locationStartLinNo, @locationBodyStart, @locationBodyEnd, @locationEndLinNo, @contentHash
            );
        ";

            insertCmd.Parameters.AddWithValue("@targetMethodId", invocation.TargetMethodId);
            insertCmd.Parameters.AddWithValue("@sourceMethodId", invocation.SourceMethodId);
            insertCmd.Parameters.AddWithValue("@guid", invocation.Guid);
            insertCmd.Parameters.AddWithValue("@isAssertion", invocation.IsAssertion);
            insertCmd.Parameters.AddWithValue("@fullString", invocation.FullString);
            insertCmd.Parameters.AddWithValue("@locationStartLinNo", invocation.Location.StartLineNumber);
            insertCmd.Parameters.AddWithValue("@locationBodyStart", invocation.Location.BodyStartPosition);
            insertCmd.Parameters.AddWithValue("@locationBodyEnd", invocation.Location.BodyEndPosition);
            insertCmd.Parameters.AddWithValue("@locationEndLinNo", invocation.Location.EndLineNumber);
            insertCmd.Parameters.AddWithValue("@contentHash", invocation.ContentHash);           


            try
            {
                await insertCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            invocation.Id = (int)newId;
        }
    }


    public async Task<List<InvocationDetails>> GetUnresolvedInvocations()
    {
        var results = new List<InvocationDetails>();

        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                i.id, i.target_method_id, i.source_method_id, i.guid, i.full_string,
                m.id, m.class_id, m.guid, m.name,
                c.id, c.file_id, c.guid, c.name,
                s.name, s.path, s.guid,
                s.analysis_project_id,
                ap.id, ap.project_path, ap.solution_id, ap.guid,
                asol.id, asol.solution_path, asol.guid
            FROM invocations AS i
            JOIN methods AS m ON i.target_method_id = m.id
            JOIN classes AS c ON m.class_id = c.id
            JOIN source_files AS s ON c.file_id = s.id
            JOIN analysis_projects AS ap ON s.analysis_project_id = ap.id
            JOIN analysis_solutions AS asol ON ap.solution_id = asol.id
            WHERE i.source_method_id = 0;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var item = new InvocationDetails
            {
                InvocationId = reader.GetInt32(0),
                TargetMethodId = reader.GetInt32(1),
                SourceMethodId = reader.GetInt32(2),
                InvocationGuid = reader.GetString(3),
                FullString = reader.GetString(4),

                MethodId = reader.GetInt32(5),
                MethodClassId = reader.GetInt32(6),
                MethodGuid = reader.GetString(7),
                MethodName = reader.GetString(8),

                ClassId = reader.GetInt32(9),
                FileId = reader.GetInt32(10),
                ClassGuid = reader.GetString(11),
                ClassName = reader.GetString(12),

                FileName = reader.GetString(13),
                FilePath = reader.GetString(14),
                FileGuid = reader.GetString(15),
                
                AnalysisProjectId = reader.GetInt32(16),

                ProjectId = reader.GetInt32(17),
                ProjectPath = reader.GetString(18),
                SolutionId = reader.GetInt32(19),
                ProjectGuid = reader.GetString(20),

                SolutionDbId = reader.GetInt32(21),
                SolutionPath = reader.GetString(22),
                SolutionGuid = reader.GetString(23)
            };

            results.Add(item);
        }

        return results;
    }

    public async Task UpdateInvocationSourceId(int invocationId, int sourceMethodId)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        UPDATE invocations
        SET source_method_id = @sourceMethodId
        WHERE id = @invocationId;
    ";

        cmd.Parameters.AddWithValue("@sourceMethodId", sourceMethodId);
        cmd.Parameters.AddWithValue("@invocationId", invocationId);

        await cmd.ExecuteNonQueryAsync();
    }
}