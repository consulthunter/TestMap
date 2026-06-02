using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Experiment;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="ExperimentRunRepository"/> using an in-memory SQLite database.
/// Covers insert, project-scoped query, active-experiment filtering, and the manual
/// field-patch in UpdateAsync (including its "default to Completed" Status behaviour).
/// </summary>
public sealed class ExperimentRunRepositoryTests
{
    /// <summary>
    /// Inserting a new experiment run returns a positive database ID and
    /// the row count increases by one.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_PersistsRun_ReturnsId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentRunRepository(db);

        var id = await repo.InsertAsync(MakeRun(projectId: 1));

        Assert.True(id > 0);
        Assert.Equal(1, await db.ExperimentRuns.CountAsync());
    }

    /// <summary>
    /// GetByProjectIdAsync returns only the runs belonging to the given project,
    /// ordered by StartTime descending.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByProjectIdAsync_ReturnsProjectRunsOrderedByStartTimeDesc()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentRunRepository(db);

        var earlier = MakeRun(projectId: 10, startedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var later   = MakeRun(projectId: 10, startedAt: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var other   = MakeRun(projectId: 99);
        await repo.InsertAsync(earlier);
        await repo.InsertAsync(later);
        await repo.InsertAsync(other);

        var runs = await repo.GetByProjectIdAsync(projectId: 10);

        Assert.Equal(2, runs.Count);
        // Most recent first
        Assert.True(runs[0].StartedAt >= runs[1].StartedAt);
        Assert.All(runs, r => Assert.Equal(10, r.ProjectId));
    }

    /// <summary>
    /// GetActiveExperimentsAsync returns only runs whose EndTime is null, i.e. not yet completed.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetActiveExperimentsAsync_ExcludesCompletedRuns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentRunRepository(db);

        var active    = MakeRun(projectId: 1);
        var completed = MakeRun(projectId: 1, completedAt: DateTime.UtcNow);
        await repo.InsertAsync(active);
        await repo.InsertAsync(completed);

        var actives = await repo.GetActiveExperimentsAsync();

        Assert.Single(actives);
        Assert.Null(actives[0].CompletedAt);
    }

    /// <summary>
    /// UpdateAsync patches the tracked entity's fields so the next read reflects the new values.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_ModifiesExistingRun()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentRunRepository(db);
        var run = MakeRun(projectId: 1);
        run.Id = await repo.InsertAsync(run);

        run.ResultsFilePath = "/results/exp1.csv";
        run.Status = "Completed";
        await repo.UpdateAsync(run);

        var updated = await repo.GetByIdAsync(run.Id);
        Assert.NotNull(updated);
        Assert.Equal("/results/exp1.csv", updated!.ResultsFilePath);
        Assert.Equal("Completed", updated.Status);
    }

    /// <summary>
    /// When UpdateAsync is called with an empty Status string it writes "Completed" to
    /// the database rather than persisting an empty value.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_DefaultsStatusToCompleted_WhenStatusIsEmpty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentRunRepository(db);
        var run = MakeRun(projectId: 1);
        run.Id = await repo.InsertAsync(run);

        run.Status = string.Empty;
        await repo.UpdateAsync(run);

        var entity = await db.ExperimentRuns.FindAsync(run.Id);
        Assert.Equal("Completed", entity!.Status);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static ExperimentRun MakeRun(
        int projectId = 1,
        DateTime? startedAt = null,
        DateTime? completedAt = null) => new()
    {
        ProjectId = projectId,
        StartedAt = startedAt ?? DateTime.UtcNow,
        CompletedAt = completedAt,
        Objective = "TestSuiteExpansion",
        CandidateSelectionStrategy = "Existing",
        ConfigurationJson = "{}",
        ResultsFilePath = string.Empty,
        CandidateLimit = 10,
        Status = "Running"
    };
}
