using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;

namespace TestMap.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
[Trait("Execution", "LocalOnly")]
public sealed class PinnedTargetSchemaTests
{
    [Fact]
    public async Task MigrateAsync_CreatesAttemptProvenanceAndIntegrityAuditSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"testmap-pinned-schema-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite($"Data Source={path}").Options;
            await using var db = new TestMapDbContext(options);
            await db.Database.MigrateAsync();
            Assert.Contains("base_commit", await ColumnsAsync(db, "generation_attempts"));
            Assert.Contains("workspace_integrity_status", await ColumnsAsync(db, "generation_attempts"));
            Assert.Contains("workspace_integrity_status", await ColumnsAsync(db, "tool_attempts"));
            foreach (var column in new[] { "target_id", "repository_identity", "requested_commit", "resolved_commit", "target_manifest_sha256", "workspace_integrity_status" })
                Assert.Contains(column, await ColumnsAsync(db, "experiment_runs"));
            Assert.Contains("checkpoint", await ColumnsAsync(db, "workspace_integrity_observations"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task<IReadOnlyList<string>> ColumnsAsync(TestMapDbContext db, string table)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{table}');";
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(1));
        return result;
    }
}
