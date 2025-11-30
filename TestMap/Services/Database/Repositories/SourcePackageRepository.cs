using Microsoft.Data.Sqlite;
using TestMap.Models;
using TestMap.Models.Code;

namespace TestMap.Services.Database.Repositories;

public class SourcePackageRepository
{
    private readonly string _dbPath;

    private readonly ProjectModel _projectModel;

    public SourcePackageRepository(ProjectModel projectModel, string dbPath)
    {
        _dbPath = dbPath;
        _projectModel = projectModel;
    }

    public async Task InsertPackageGetId(PackageModel package)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        // First, check if the package already exists
        var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = @"
            SELECT id, guid FROM source_packages
            WHERE content_hash = @hash
        ";

        checkCmd.Parameters.AddWithValue("@hash", package.ContentHash);


        using var reader = await checkCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int id = reader.GetInt16(0);
            var guid = reader.GetString(1);

            package.Id = id;
            package.Guid = guid;
        }
        else
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
            INSERT INTO source_packages (
                analysis_project_id, guid, package_name, package_path, content_hash
            ) VALUES (
                @analysisProjectId, @guid, @packageName, @packagePath, @contentHash
            );
        ";

            insertCmd.Parameters.AddWithValue("@analysisProjectId", package.ProjectId);
            insertCmd.Parameters.AddWithValue("@guid", package.Guid);
            insertCmd.Parameters.AddWithValue("@packageName", package.Name);
            insertCmd.Parameters.AddWithValue("@packagePath", package.Path);
            insertCmd.Parameters.AddWithValue("@contentHash", package.ContentHash);


            await insertCmd.ExecuteNonQueryAsync();

            var lastIdCmd = conn.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid();";
            var newId = (long)await lastIdCmd.ExecuteScalarAsync();
            package.Id = (int)newId;
        }
    }

    public async Task<int> FindPackage(string name)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT p.id
        FROM source_packages AS p
        WHERE p.package_name = @name
        LIMIT 1;
    ";

        cmd.Parameters.AddWithValue("@name", name.Replace("-", "_"));

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return reader.GetInt32(0); // m.guid

        return 0; // not found
    }
}