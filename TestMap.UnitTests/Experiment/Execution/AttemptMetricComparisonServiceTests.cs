using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Entities.MutationTesting;
using TestMap.Persistence.Ef.Entities.Testing;
using TestMap.Services.Experiment.Execution;

namespace TestMap.UnitTests.Experiment.Execution;

public sealed class AttemptMetricComparisonServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompareAsync_UsesSameMemberAcrossBaselineAndPostRuns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedRunsAsync(db);
        await AddCoverageAsync(db, 10, 77, 0.25, aggregateRate: 0.9);
        await AddCoverageAsync(db, 20, 77, 0.45, aggregateRate: 0.1);
        await AddMutationAsync(db, 10, 55, "Project", "src/App.csproj");
        await AddMutationAsync(db, 20, 60, "Project", "src/App.csproj");

        var result = await new AttemptMetricComparisonService(db).CompareAsync(77, 10, 20);

        Assert.Equal("Paired", result.CoverageStatus);
        Assert.Equal(0.25, result.CoverageBefore);
        Assert.Equal(0.45, result.CoverageAfter);
        Assert.Equal(0.20, result.CoverageDelta!.Value, 8);
        Assert.Equal("Paired", result.MutationStatus);
        Assert.Equal(5, result.MutationScoreDelta);
        Assert.Equal("Complete", result.ImpactStatus);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompareAsync_DoesNotSubstituteAggregateCoverageForMissingMemberCoverage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedRunsAsync(db);
        await AddCoverageAsync(db, 10, 77, 0.25, aggregateRate: 0.9);
        await AddCoverageAsync(db, 20, 88, 0.45, aggregateRate: 0.7);

        var result = await new AttemptMetricComparisonService(db).CompareAsync(77, 10, 20);

        Assert.Equal("MissingPost", result.CoverageStatus);
        Assert.Null(result.CoverageAfter);
        Assert.Null(result.CoverageDelta);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompareAsync_RejectsMutationScoresFromDifferentScopes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedRunsAsync(db);
        await AddMutationAsync(db, 10, 55, "Project", "src/App.csproj");
        await AddMutationAsync(db, 20, 80, "Solution", "src/App.csproj");

        var result = await new AttemptMetricComparisonService(db).CompareAsync(77, 10, 20);

        Assert.Equal("ScopeMismatch", result.MutationStatus);
        Assert.Null(result.MutationScoreDelta);
        Assert.Equal("Missing", result.ImpactStatus);
    }

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task SeedRunsAsync(TestMapDbContext db)
    {
        db.Projects.Add(new ProjectEntity { Id = 1, Owner = "owner", RepoName = "repo" });
        db.TestRuns.AddRange(
            new TestRunEntity { Id = 10, ProjectId = 1, RunId = "baseline" },
            new TestRunEntity { Id = 20, ProjectId = 1, RunId = "post" });
        await db.SaveChangesAsync();
    }

    private static async Task AddCoverageAsync(
        TestMapDbContext db,
        int testRunId,
        int memberId,
        double memberRate,
        double aggregateRate)
    {
        var report = new CoverageReportEntity
        {
            ProjectId = 1,
            TestRunId = testRunId,
            LineRate = aggregateRate,
            MeasurementPolicyVersion = "coverage-integrity-v1",
            HasUsableCoverage = true,
            CollectionStatus = "Mapped"
        };
        db.CoverageReports.Add(report);
        await db.SaveChangesAsync();
        db.MemberCoverages.Add(new MemberCoverageEntity
        {
            CoverageReportId = report.Id,
            MemberId = memberId,
            AttributionStatus = "Mapped",
            LineRate = memberRate
        });
        await db.SaveChangesAsync();
    }

    private static async Task AddMutationAsync(
        TestMapDbContext db,
        int testRunId,
        double score,
        string scopeKind,
        string sourceProjectPath)
    {
        db.MutationTestingReports.Add(new MutationTestingReportEntity
        {
            ProjectId = 1,
            TestRunId = testRunId,
            MutationScore = score,
            ScopeKind = scopeKind,
            SourceProjectPath = sourceProjectPath,
            TestProjectPath = "tests/App.Tests.csproj",
            TargetFramework = "net10.0"
        });
        await db.SaveChangesAsync();
    }
}
