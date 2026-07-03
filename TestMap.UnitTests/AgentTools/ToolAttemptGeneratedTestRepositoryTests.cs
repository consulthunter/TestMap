using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.AgentTools;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.AgentTools;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Tests for <see cref="ToolAttemptGeneratedTestRepository"/> using an in-memory SQLite database.
/// </summary>
public sealed class ToolAttemptGeneratedTestRepositoryTests
{
    /// <summary>
    /// InsertAsync persists a row and returns a positive auto-assigned id.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_Persists_ReturnsPositiveId()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId) = await SeedExperimentGraphAsync(db);
        var attemptId = await InsertAttemptAsync(db, runId, candidateId);
        var repo = new ToolAttemptGeneratedTestRepository(db);
        var row = new ToolAttemptGeneratedTest { ToolAttemptId = attemptId, MemberId = 42 };

        // Act
        var id = await repo.InsertAsync(row);

        // Assert
        Assert.True(id > 0);
        Assert.Equal(1, await db.ToolAttemptGeneratedTests.CountAsync());
    }

    /// <summary>
    /// GetByAttemptIdAsync returns all rows for the specified attempt id.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByAttemptIdAsync_ReturnsRowsForAttempt()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId) = await SeedExperimentGraphAsync(db);
        var attempt1Id = await InsertAttemptAsync(db, runId, candidateId);
        var attempt2Id = await InsertAttemptAsync(db, runId, candidateId);
        var repo = new ToolAttemptGeneratedTestRepository(db);

        await repo.InsertAsync(new ToolAttemptGeneratedTest { ToolAttemptId = attempt1Id, MemberId = 10 });
        await repo.InsertAsync(new ToolAttemptGeneratedTest { ToolAttemptId = attempt1Id, MemberId = 20, MappingId = 5 });
        await repo.InsertAsync(new ToolAttemptGeneratedTest { ToolAttemptId = attempt2Id, MemberId = 30 });

        // Act
        var results = await repo.GetByAttemptIdAsync(attempt1Id);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(attempt1Id, r.ToolAttemptId));
        Assert.Contains(results, r => r.MemberId == 10 && r.MappingId == null);
        Assert.Contains(results, r => r.MemberId == 20 && r.MappingId == 5);
    }

    /// <summary>
    /// InsertManyAsync inserts all rows in a single save and skips empty lists.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertManyAsync_InsertsAllRows_SkipsEmpty()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId) = await SeedExperimentGraphAsync(db);
        var attemptId = await InsertAttemptAsync(db, runId, candidateId);
        var repo = new ToolAttemptGeneratedTestRepository(db);

        var rows = new[]
        {
            new ToolAttemptGeneratedTest { ToolAttemptId = attemptId, MemberId = 1 },
            new ToolAttemptGeneratedTest { ToolAttemptId = attemptId, MemberId = 2 },
            new ToolAttemptGeneratedTest { ToolAttemptId = attemptId, MemberId = 3 }
        };

        // Act — insert 3 rows
        await repo.InsertManyAsync(rows);

        // Assert
        Assert.Equal(3, await db.ToolAttemptGeneratedTests.CountAsync());

        // Act — insert empty list (should not throw)
        await repo.InsertManyAsync([]);

        // Assert — count unchanged
        Assert.Equal(3, await db.ToolAttemptGeneratedTests.CountAsync());
    }

    /// <summary>
    /// Round-trip: MappingId null and non-null survive the insert/retrieve cycle.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_RoundTrips_MappingIdNullAndNonNull()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var (runId, candidateId) = await SeedExperimentGraphAsync(db);
        var attemptId = await InsertAttemptAsync(db, runId, candidateId);
        var repo = new ToolAttemptGeneratedTestRepository(db);

        // Act
        var idWithNull = await repo.InsertAsync(
            new ToolAttemptGeneratedTest { ToolAttemptId = attemptId, MemberId = 7, MappingId = null });
        var idWithValue = await repo.InsertAsync(
            new ToolAttemptGeneratedTest { ToolAttemptId = attemptId, MemberId = 8, MappingId = 99 });

        // Assert
        db.ChangeTracker.Clear();
        var results = await repo.GetByAttemptIdAsync(attemptId);
        var withNull = results.First(r => r.Id == idWithNull);
        var withValue = results.First(r => r.Id == idWithValue);

        Assert.Null(withNull.MappingId);
        Assert.Equal(99, withValue.MappingId);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<(int ExperimentRunId, int CandidateMethodId)> SeedExperimentGraphAsync(
        TestMapDbContext db)
    {
        var run = new ExperimentRunEntity
        {
            ProjectId = 1,
            StartTime = DateTime.UtcNow,
            Objective = "TestSuiteExpansion",
            CandidateSelectionStrategy = "Existing",
            Configuration = "{}",
            ResultsFilePath = string.Empty,
            Status = "Running"
        };
        db.ExperimentRuns.Add(run);
        await db.SaveChangesAsync();

        var candidate = new CandidateMethodEntity
        {
            ExperimentRunId = run.Id,
            SourceMemberId = 10,
            SourceMethodName = "Calculate",
            SourceMethodSignature = "public int Calculate()"
        };
        db.CandidateMethods.Add(candidate);
        await db.SaveChangesAsync();

        return (run.Id, candidate.Id);
    }

    private static async Task<int> InsertAttemptAsync(
        TestMapDbContext db,
        int experimentRunId,
        int candidateMethodId)
    {
        var repo = new ToolAttemptRepository(db);
        return await repo.InsertAsync(new TestMap.Models.AgentTools.ToolAttempt
        {
            ExperimentRunId = experimentRunId,
            CandidateMethodId = candidateMethodId,
            ToolId = "codex",
            RunStatus = TestMap.Models.AgentTools.ToolRunStatus.Planned,
            StartedAt = DateTime.UtcNow,
            TimeoutSeconds = 2700
        });
    }
}
