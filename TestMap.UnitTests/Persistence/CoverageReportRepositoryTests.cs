using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Coverage;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Tests for <see cref="CoverageReportRepository"/> using an in-memory SQLite database.
/// Covers insert-or-update semantics (keyed on projectId + Timestamp), the SanitizeDouble
/// guard that converts NaN/Infinity to 0, and the GetLatestByProjectIdAsync ordering.
/// </summary>
public sealed class CoverageReportRepositoryTests
{
    /// <summary>
    /// InsertOrUpdateAsync with a new project+timestamp combination inserts the row
    /// and returns a positive ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_NewReport_InsertsAndReturnsPositiveId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CoverageReportRepository(db);

        var id = await repo.InsertOrUpdateAsync(MakeReport(lineRate: 0.75, timestamp: 1_000_000), projectId: 1);

        Assert.True(id > 0);
        Assert.Equal(1, await db.CoverageReports.CountAsync());
    }

    /// <summary>
    /// Calling InsertOrUpdateAsync twice with the same projectId and Timestamp returns
    /// the same ID without inserting a duplicate row (idempotent).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_SameProjectAndTimestamp_IsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CoverageReportRepository(db);
        var report = MakeReport(lineRate: 0.80, timestamp: 2_000_000);

        var firstId  = await repo.InsertOrUpdateAsync(report, projectId: 2);
        var secondId = await repo.InsertOrUpdateAsync(report, projectId: 2);

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await db.CoverageReports.CountAsync());
    }

    /// <summary>
    /// When a report with the same key already exists but the LineRate has changed,
    /// InsertOrUpdateAsync updates the row and still returns the original ID.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_ChangedLineRate_UpdatesExistingRow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CoverageReportRepository(db);

        var firstId = await repo.InsertOrUpdateAsync(MakeReport(lineRate: 0.60, timestamp: 3_000_000), projectId: 3);
        await repo.InsertOrUpdateAsync(MakeReport(lineRate: 0.90, timestamp: 3_000_000), projectId: 3);

        var entity = await db.CoverageReports.FindAsync(firstId);
        Assert.Equal(0.90, entity!.LineRate, precision: 5);
        Assert.Equal(1, await db.CoverageReports.CountAsync());
    }

    /// <summary>
    /// NaN and Infinity line rates are sanitized to 0.0 by SanitizeDouble before being
    /// written to the database, preventing constraint violations on REAL columns.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_NaNLineRate_SanitizesToZero()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CoverageReportRepository(db);

        var report = MakeReport(lineRate: double.NaN, timestamp: 4_000_000);
        var id = await repo.InsertOrUpdateAsync(report, projectId: 4);

        var entity = await db.CoverageReports.FindAsync(id);
        Assert.Equal(0.0, entity!.LineRate);
    }

    /// <summary>
    /// GetLatestByProjectIdAsync returns the report with the most recent CreatedAt
    /// for the specified project, ignoring reports from other projects.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetLatestByProjectIdAsync_ReturnsMostRecentReport()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CoverageReportRepository(db);

        // Two reports for project 5 (different timestamps → different rows)
        await repo.InsertOrUpdateAsync(MakeReport(lineRate: 0.50, timestamp: 100), projectId: 5);
        await repo.InsertOrUpdateAsync(MakeReport(lineRate: 0.85, timestamp: 200), projectId: 5);
        // One report for a different project — should not be returned
        await repo.InsertOrUpdateAsync(MakeReport(lineRate: 0.99, timestamp: 300), projectId: 99);

        var latest = await repo.GetLatestByProjectIdAsync(projectId: 5);

        // The report inserted last (highest Id / latest CreatedAt) should be returned.
        // Both have valid line rates, so the one with the higher Id (timestamp 200) wins.
        Assert.NotNull(latest);
        Assert.Equal(0.85, latest!.LineRate, precision: 5);
    }

    /// <summary>
    /// HasCoverageReportsAsync returns true when at least one report exists for the project,
    /// and false when the project has no reports.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task HasCoverageReportsAsync_ReturnsTrueWhenReportsExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var repo = new CoverageReportRepository(db);

        await repo.InsertOrUpdateAsync(MakeReport(timestamp: 500), projectId: 6);

        Assert.True(await repo.HasCoverageReportsAsync(projectId: 6));
        Assert.False(await repo.HasCoverageReportsAsync(projectId: 999));
    }

    // ─── Infrastructure ───────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static CoverageReportModel MakeReport(
        double lineRate = 0.75,
        double branchRate = 0.60,
        long timestamp = 1_000_000) => new()
    {
        LineRate = lineRate,
        BranchRate = branchRate,
        ComplexityRaw = "5",
        Version = "1.9",
        Timestamp = timestamp,
        LinesCovered = 100,
        LinesValid = 133,
        BranchesCovered = 40,
        BranchesValid = 66
    };
}
