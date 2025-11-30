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

    public async Task GetImports()
    {
        var results = new List<ImportModel>();

        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                i.id, i.target_method_id, i.source_method_id, i.guid, i.full_string,
                m.id, m.class_id, m.guid, m.name,
                c.id, c.file_id, c.guid, c.name,
                s.name, s.path, s.package_id, s.guid,
                sp.id, sp.package_name, sp.analysis_project_id, sp.guid,
                ap.id, ap.project_path, ap.solution_id, ap.guid,
                asol.id, asol.solution_path, asol.guid
            FROM invocations AS i
            JOIN methods AS m ON i.target_method_id = m.id
            JOIN classes AS c ON m.class_id = c.id
            JOIN source_files AS s ON c.file_id = s.id
            JOIN source_packages AS sp ON s.package_id = sp.id
            JOIN analysis_projects AS ap ON sp.analysis_project_id = ap.id
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
                PackageId = reader.GetInt32(15),
                FileGuid = reader.GetString(16),

                SourcePackageId = reader.GetInt32(17),
                PackageName = reader.GetString(18),
                AnalysisProjectId = reader.GetInt32(19),
                PackageGuid = reader.GetString(20),

                ProjectId = reader.GetInt32(21),
                ProjectPath = reader.GetString(22),
                SolutionId = reader.GetInt32(23),
                ProjectGuid = reader.GetString(24),

                SolutionDbId = reader.GetInt32(25),
                SolutionPath = reader.GetString(26),
                SolutionGuid = reader.GetString(27)
            };

            // results.Add(item);
        }

        // return results;
    }
}