using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;

namespace TestMap.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
[Trait("Execution", "LocalOnly")]
public sealed class PinnedProjectProvenanceSchemaTests
{
    [Fact]
    public async Task MigrateAsync_BlankDatabase_CreatesPinnedProjectColumnsAndIndexes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-pinned-project-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite($"Data Source={path}").Options;
            await using var db = new TestMapDbContext(options);
            await db.Database.MigrateAsync();
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync();

            await using var columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = "PRAGMA table_info('projects');";
            var columns = new List<string>();
            await using (var reader = await columnsCommand.ExecuteReaderAsync())
                while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            foreach (var expected in new[] { "target_id", "repository_identity", "requested_commit", "resolved_commit", "target_manifest_sha256", "target_source_sha256", "materialized_at_utc", "provenance_policy_version" })
                Assert.Contains(expected, columns);

            await using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='projects';";
            var indexes = new List<string>();
            await using (var reader = await indexCommand.ExecuteReaderAsync())
                while (await reader.ReadAsync()) indexes.Add(reader.GetString(0));
            Assert.Contains("IX_projects_target_id", indexes);
            Assert.Contains("IX_projects_repository_identity_resolved_commit", indexes);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
