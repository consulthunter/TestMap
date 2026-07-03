using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="ExperimentMatrixWorkItemRepository"/> using an in-memory SQLite database.
/// Covers upsert semantics (insert vs update by StableKey), experiment-run-scoped query,
/// and the status-driven field transitions in UpdateStatusAsync.
/// </summary>
public sealed class ExperimentMatrixWorkItemRepositoryTests
{
    /// <summary>
    /// UpsertAsync with a new StableKey inserts the item and returns a positive ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpsertAsync_NewStableKey_InsertsItemAndReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentMatrixWorkItemRepository(db);
        var (runId, methodId) = await SeedGraphAsync(db);

        var id = await repo.UpsertAsync(MakeItem(runId, methodId, stableKey: "key-001"));

        Assert.True(id > 0);
        Assert.Equal(1, await db.ExperimentMatrixWorkItems.CountAsync());
    }

    /// <summary>
    /// UpsertAsync called a second time with the same StableKey updates the status fields
    /// and returns the original row's ID rather than creating a duplicate.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpsertAsync_ExistingStableKey_UpdatesStatusAndReturnsSameId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentMatrixWorkItemRepository(db);
        var (runId, methodId) = await SeedGraphAsync(db);

        var firstId = await repo.UpsertAsync(MakeItem(runId, methodId, stableKey: "key-001",
            status: ExperimentMatrixWorkItemStatus.Pending));

        var updated = MakeItem(runId, methodId, stableKey: "key-001",
            status: ExperimentMatrixWorkItemStatus.Completed);
        updated.CompletedAt = DateTime.UtcNow;
        var secondId = await repo.UpsertAsync(updated);

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await db.ExperimentMatrixWorkItems.CountAsync());

        var entity = await db.ExperimentMatrixWorkItems.FindAsync(firstId);
        Assert.Equal(ExperimentMatrixWorkItemStatus.Completed, entity!.Status);
        Assert.NotNull(entity.CompletedAt);
    }

    /// <summary>
    /// GetByExperimentRunAsync returns only the items belonging to the given run,
    /// ordered by ID ascending.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByExperimentRunAsync_ReturnsItemsForRunOrderedById()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentMatrixWorkItemRepository(db);
        var (runId, methodId) = await SeedGraphAsync(db);
        var (otherRunId, otherMethodId) = await SeedGraphAsync(db);

        await repo.UpsertAsync(MakeItem(runId, methodId, stableKey: "key-a"));
        await repo.UpsertAsync(MakeItem(runId, methodId, stableKey: "key-b"));
        await repo.UpsertAsync(MakeItem(otherRunId, otherMethodId, stableKey: "key-other"));

        var items = await repo.GetByExperimentRunAsync(runId);

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(runId, i.ExperimentRunId));
        Assert.True(items[0].Id <= items[1].Id);
    }

    /// <summary>
    /// GetByStableKeyAsync returns the item matching the given key, or null when not found.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByStableKeyAsync_FindsByKey_ReturnsNullWhenAbsent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentMatrixWorkItemRepository(db);
        var (runId, methodId) = await SeedGraphAsync(db);
        await repo.UpsertAsync(MakeItem(runId, methodId, stableKey: "known-key"));

        var found = await repo.GetByStableKeyAsync("known-key");
        var missing = await repo.GetByStableKeyAsync("no-such-key");

        Assert.NotNull(found);
        Assert.Equal("known-key", found!.StableKey);
        Assert.Null(missing);
    }

    /// <summary>
    /// UpdateStatusAsync with Running sets StartedAt (if not already set) and
    /// updates LastHeartbeatAt, but does not set CompletedAt.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateStatusAsync_Running_SetsStartedAtAndHeartbeat()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentMatrixWorkItemRepository(db);
        var (runId, methodId) = await SeedGraphAsync(db);
        var id = await repo.UpsertAsync(MakeItem(runId, methodId, stableKey: "key-r"));

        await repo.UpdateStatusAsync(id, ExperimentMatrixWorkItemStatus.Running);

        var entity = await db.ExperimentMatrixWorkItems.FindAsync(id);
        Assert.Equal(ExperimentMatrixWorkItemStatus.Running, entity!.Status);
        Assert.NotNull(entity.StartedAt);
        Assert.NotNull(entity.LastHeartbeatAt);
        Assert.Null(entity.CompletedAt);
    }

    /// <summary>
    /// UpdateStatusAsync with Completed sets CompletedAt and LastHeartbeatAt.
    /// Passing an errorMessage stores it on the entity.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateStatusAsync_Completed_SetsCompletedAtAndStoresError()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new ExperimentMatrixWorkItemRepository(db);
        var (runId, methodId) = await SeedGraphAsync(db);
        var id = await repo.UpsertAsync(MakeItem(runId, methodId, stableKey: "key-c"));

        await repo.UpdateStatusAsync(id, ExperimentMatrixWorkItemStatus.Failed,
            errorMessage: "Something went wrong");

        var entity = await db.ExperimentMatrixWorkItems.FindAsync(id);
        Assert.Equal(ExperimentMatrixWorkItemStatus.Failed, entity!.Status);
        Assert.NotNull(entity.CompletedAt);
        Assert.Equal("Something went wrong", entity.ErrorMessage);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    /// <summary>
    /// Seeds ExperimentRun → CandidateMethod; returns (runId, methodId).
    /// ExperimentMatrixWorkItemEntity has FK nav-props to both so real parents are required.
    /// </summary>
    private static async Task<(int RunId, int MethodId)> SeedGraphAsync(TestMapDbContext db)
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

    private static ExperimentMatrixWorkItem MakeItem(
        int experimentRunId,
        int candidateMethodId,
        string stableKey = "key",
        string status = ExperimentMatrixWorkItemStatus.Pending) => new()
    {
        ExperimentRunId = experimentRunId,
        CandidateMethodId = candidateMethodId,
        MemberId = 1,
        StableKey = stableKey,
        Status = status,
        Provider = Models.Configuration.AiProviders.AiProvider.OpenAi,
        ModelName = "gpt-4o",
        Objective = Models.Configuration.Testing.Generation.TestGenerationObjective.TestSuiteExpansion,
        Approach = Models.Configuration.Testing.Generation.TestGenerationApproach.MetricsDriven,
        ContextMode = Models.Configuration.Testing.Generation.GenerationContextMode.ChainedHistory,
        BudgetMode = Models.Configuration.Testing.Generation.GenerationBudgetMode.PassAt1,
        AblationVariantId = string.Empty,
        StepConfigJson = string.Empty,
        CreatedAt = DateTime.UtcNow,
        ErrorMessage = string.Empty
    };
}
