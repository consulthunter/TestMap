using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Experiment;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Integration tests for <see cref="CandidateInventoryRepository"/> using an in-memory SQLite
/// database. Covers insert, replace, isolation between projects/strategies, count queries,
/// eligibility filtering, and enum round-tripping.
/// </summary>
public sealed class CandidateInventoryRepositoryTests
{
    /// <summary>
    /// Inserting items via ReplaceForProjectStrategyAsync returns the persisted domain objects
    /// with auto-assigned database IDs greater than zero.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReplaceForProjectStrategyAsync_InsertsItemsAndReturnsWithAssignedIds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        var repo = new CandidateInventoryRepository(db);

        var items = new[]
        {
            new CandidateInventoryItem
            {
                ProjectId = 1,
                SourceMemberId = 10,
                SourceMethodName = "Calculate",
                SourceMethodSignature = "public int Calculate(int x)",
                SelectionStrategy = TargetSelectionStrategy.Existing,
                IsExperimentEligible = true,
                InitialCoverage = 0.5,
                ComplexityScore = 2.0,
                TestState = CandidateTestState.NeedsTestImprovement,
                RecommendedAction = CandidateActionKind.GenerateNewTest
            }
        };

        var result = await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.Existing, items);

        var persisted = Assert.Single(result);
        Assert.True(persisted.Id > 0);
        Assert.Equal(1, persisted.ProjectId);
        Assert.Equal(10, persisted.SourceMemberId);
        Assert.Equal("Calculate", persisted.SourceMethodName);
        Assert.Equal(TargetSelectionStrategy.Existing, persisted.SelectionStrategy);
        Assert.True(persisted.IsExperimentEligible);
        Assert.Equal(0.5, persisted.InitialCoverage);
        Assert.Equal(2.0, persisted.ComplexityScore);
    }

    /// <summary>
    /// A second call to ReplaceForProjectStrategyAsync for the same project+strategy removes the
    /// previously inserted rows and writes the new batch — the final count equals the new batch size.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReplaceForProjectStrategyAsync_SecondCallReplacesFirstBatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        var repo = new CandidateInventoryRepository(db);

        // First batch: 2 items
        await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.Existing,
        [
            MakeItem(1, 10, "OldMethod1"),
            MakeItem(1, 11, "OldMethod2")
        ]);
        Assert.Equal(2, await db.CandidateInventory.CountAsync());

        // Second batch: 1 item — replaces the 2 old ones
        await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.Existing,
        [
            MakeItem(1, 20, "NewMethod")
        ]);

        var all = await db.CandidateInventory.ToListAsync();
        var single = Assert.Single(all);
        Assert.Equal(20, single.SourceMemberId);
        Assert.Equal("NewMethod", single.SourceMethodName);
    }

    /// <summary>
    /// Replacing rows for project 1 does not delete or modify rows belonging to project 99.
    /// The inventory is strictly isolated per project.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReplaceForProjectStrategyAsync_DoesNotTouchOtherProjectsRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        var repo = new CandidateInventoryRepository(db);

        await repo.ReplaceForProjectStrategyAsync(99, TargetSelectionStrategy.Existing,
            [MakeItem(99, 5, "OtherProjectMethod")]);

        await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.Existing,
            [MakeItem(1, 10, "ProjectOneMethod")]);

        Assert.Equal(1, await db.CandidateInventory.CountAsync(x => x.ProjectId == 99));
        Assert.Equal(1, await db.CandidateInventory.CountAsync(x => x.ProjectId == 1));
    }

    /// <summary>
    /// Replacing rows for the Existing strategy does not delete or modify rows that were
    /// inserted under the RiskWeighted strategy for the same project.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReplaceForProjectStrategyAsync_DoesNotTouchOtherStrategyRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        var repo = new CandidateInventoryRepository(db);

        await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.Existing,
            [MakeItem(1, 10, "ExistingMethod", strategy: TargetSelectionStrategy.Existing)]);

        await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.RiskWeighted,
            [MakeItem(1, 20, "RiskMethod", strategy: TargetSelectionStrategy.RiskWeighted)]);

        Assert.Equal(1, await db.CandidateInventory.CountAsync(x =>
            x.SelectionStrategy == "Existing"));
        Assert.Equal(1, await db.CandidateInventory.CountAsync(x =>
            x.SelectionStrategy == "RiskWeighted"));
    }

    /// <summary>
    /// Calling ReplaceForProjectStrategyAsync with an empty collection removes all existing
    /// rows for that project+strategy and returns an empty list.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReplaceForProjectStrategyAsync_EmptyListClearsAllExistingRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        var repo = new CandidateInventoryRepository(db);

        await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.Existing,
        [
            MakeItem(1, 10, "M1"),
            MakeItem(1, 11, "M2")
        ]);
        Assert.Equal(2, await db.CandidateInventory.CountAsync());

        var result = await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.Existing, []);

        Assert.Empty(result);
        Assert.Equal(0, await db.CandidateInventory.CountAsync());
    }

    /// <summary>
    /// CountAsync returns the total number of items for the given project+strategy combination,
    /// correctly excluding items from other projects and other strategies.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CountAsync_ReturnsCorrectCountForProjectAndStrategy()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        var repo = new CandidateInventoryRepository(db);

        await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.Existing,
        [
            MakeItem(1, 10, "M1"),
            MakeItem(1, 11, "M2"),
            MakeItem(1, 12, "M3")
        ]);
        await repo.ReplaceForProjectStrategyAsync(2, TargetSelectionStrategy.Existing,
            [MakeItem(2, 20, "Other")]);

        Assert.Equal(3, await repo.CountAsync(1, TargetSelectionStrategy.Existing));
        Assert.Equal(1, await repo.CountAsync(2, TargetSelectionStrategy.Existing));
        Assert.Equal(0, await repo.CountAsync(1, TargetSelectionStrategy.RiskWeighted));
    }

    /// <summary>
    /// CountEligibleAsync counts only items where IsExperimentEligible is true, ignoring
    /// ineligible candidates even when they belong to the same project and strategy.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CountEligibleAsync_CountsOnlyEligibleItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        var repo = new CandidateInventoryRepository(db);

        await repo.ReplaceForProjectStrategyAsync(1, TargetSelectionStrategy.Existing,
        [
            MakeItem(1, 10, "Eligible1", isEligible: true),
            MakeItem(1, 11, "Eligible2", isEligible: true),
            MakeItem(1, 12, "Ineligible", isEligible: false)
        ]);

        Assert.Equal(3, await repo.CountAsync(1, TargetSelectionStrategy.Existing));
        Assert.Equal(2, await repo.CountEligibleAsync(1, TargetSelectionStrategy.Existing));
    }

    /// <summary>
    /// Enum fields SelectionStrategy, TestState, and RecommendedAction are stored as their
    /// string names in the database and correctly parsed back to the original enum values
    /// when the domain object is reconstructed from the entity.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ReplaceForProjectStrategyAsync_EnumFieldsRoundtripCorrectly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        var repo = new CandidateInventoryRepository(db);

        var item = new CandidateInventoryItem
        {
            ProjectId = 1,
            SourceMemberId = 10,
            SourceMethodName = "Process",
            SelectionStrategy = TargetSelectionStrategy.MetricDrivenImprovement,
            TestState = CandidateTestState.NeedsTestImprovement,
            RecommendedAction = CandidateActionKind.ImproveExistingTest,
            IsExperimentEligible = true
        };

        var result = await repo.ReplaceForProjectStrategyAsync(
            1, TargetSelectionStrategy.MetricDrivenImprovement, [item]);

        var returned = Assert.Single(result);
        Assert.Equal(TargetSelectionStrategy.MetricDrivenImprovement, returned.SelectionStrategy);
        Assert.Equal(CandidateTestState.NeedsTestImprovement, returned.TestState);
        Assert.Equal(CandidateActionKind.ImproveExistingTest, returned.RecommendedAction);
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static CandidateInventoryItem MakeItem(
        int projectId,
        int sourceMemberId,
        string methodName,
        bool isEligible = true,
        TargetSelectionStrategy strategy = TargetSelectionStrategy.Existing) =>
        new CandidateInventoryItem
        {
            ProjectId = projectId,
            SourceMemberId = sourceMemberId,
            SourceMethodName = methodName,
            SourceMethodSignature = $"public void {methodName}()",
            SelectionStrategy = strategy,
            IsExperimentEligible = isEligible
        };
}
