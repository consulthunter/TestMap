using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="CandidateMethodRepository"/> using an in-memory SQLite database.
/// Covers insert, experiment-scoped queries, the "unprocessed" filter that checks for the
/// absence of generation attempts, and the update/delete lifecycle.
/// </summary>
public sealed class CandidateMethodRepositoryTests
{
    /// <summary>
    /// InsertAsync persists the candidate method and returns a positive auto-assigned ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertAsync_PersistsCandidateMethod_ReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CandidateMethodRepository(db);
        var expRunId = await InsertExperimentRunAsync(db);

        var id = await repo.InsertAsync(MakeMethod(experimentRunId: expRunId));

        Assert.True(id > 0);
        Assert.Equal(1, await db.CandidateMethods.CountAsync());
    }

    /// <summary>
    /// GetByExperimentRunIdAsync returns only the methods associated with the given
    /// experiment run, sorted alphabetically by source method name.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByExperimentRunIdAsync_ReturnsSortedMethodsForExperiment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CandidateMethodRepository(db);
        var expRunId = await InsertExperimentRunAsync(db);
        var otherExpRunId = await InsertExperimentRunAsync(db);

        await repo.InsertAsync(MakeMethod(experimentRunId: expRunId, name: "Zebra"));
        await repo.InsertAsync(MakeMethod(experimentRunId: expRunId, name: "Alpha"));
        await repo.InsertAsync(MakeMethod(experimentRunId: otherExpRunId, name: "OtherExp"));

        var methods = await repo.GetByExperimentRunIdAsync(experimentRunId: expRunId);

        Assert.Equal(2, methods.Count);
        Assert.Equal("Alpha", methods[0].MethodName);
        Assert.Equal("Zebra", methods[1].MethodName);
    }

    /// <summary>
    /// GetUnprocessedAsync returns only methods that have no generation attempts,
    /// filtering out methods that already have at least one attempt recorded.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetUnprocessedAsync_ExcludesMethodsWithAttempts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CandidateMethodRepository(db);
        var expRunId = await InsertExperimentRunAsync(db);

        var unprocessedId = await repo.InsertAsync(MakeMethod(experimentRunId: expRunId, name: "Unprocessed"));
        var processedId   = await repo.InsertAsync(MakeMethod(experimentRunId: expRunId, name: "Processed"));

        // Add an attempt only for the "processed" method
        db.GenerationAttempts.Add(MakeAttempt(candidateMethodId: processedId));
        await db.SaveChangesAsync();

        var unprocessed = await repo.GetUnprocessedAsync(experimentRunId: expRunId);

        var single = Assert.Single(unprocessed);
        Assert.Equal(unprocessedId, single.Id);
        Assert.Equal("Unprocessed", single.MethodName);
    }

    /// <summary>
    /// UpdateAsync reflects the changed domain object so subsequent reads return the
    /// updated field values.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_ModifiesExistingMethod()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CandidateMethodRepository(db);
        var expRunId = await InsertExperimentRunAsync(db);
        var method = MakeMethod(experimentRunId: expRunId, name: "Original");
        method.Id = await repo.InsertAsync(method);

        method.MethodName = "Renamed";
        await repo.UpdateAsync(method);

        var updated = await repo.GetByIdAsync(method.Id);
        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated!.MethodName);
    }

    /// <summary>
    /// DeleteAsync removes the method so subsequent reads return null and the table is empty.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_RemovesMethod()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CandidateMethodRepository(db);
        var expRunId = await InsertExperimentRunAsync(db);
        var id = await repo.InsertAsync(MakeMethod(experimentRunId: expRunId));

        await repo.DeleteAsync(id);

        Assert.Null(await repo.GetByIdAsync(id));
        Assert.Equal(0, await db.CandidateMethods.CountAsync());
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<int> InsertExperimentRunAsync(TestMapDbContext db)
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
        return run.Id;
    }

    private static CandidateMethod MakeMethod(int experimentRunId, string name = "Calculate") => new()
    {
        ExperimentRunId = experimentRunId,
        MemberId = 100,
        MethodName = name,
        Signature = $"public void {name}()"
    };

    private static GenerationAttemptEntity MakeAttempt(int candidateMethodId) => new()
    {
        CandidateMethodId = candidateMethodId,
        ProviderName = "OpenAi",
        ModelName = "gpt-4o",
        Strategy = string.Empty,
        BudgetMode = "PassAt1",
        Objective = "TestSuiteExpansion",
        GenerationApproach = "MetricsDriven",
        ContextMode = "ChainedHistory",
        StartTime = DateTime.UtcNow,
        Status = "Completed",
        FailureKind = "None",
        FailureStage = string.Empty,
        FailureCategory = string.Empty,
        ErrorMessage = string.Empty,
        StepConfigJson = string.Empty,
        EffectiveProfileJson = string.Empty,
        EffectiveProfileHash = string.Empty,
        RuleDecisionSnapshotJson = string.Empty,
        MetricsPath = string.Empty,
        AblationVariantId = string.Empty
    };
}
