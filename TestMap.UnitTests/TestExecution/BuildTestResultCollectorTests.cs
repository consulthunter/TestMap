using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Entities.Testing;
using TestMap.Persistence.Ef.Repositories.Code;
using TestMap.Persistence.Ef.Repositories.Coverage;
using TestMap.Persistence.Ef.Repositories.MutationTesting;
using TestMap.Services.TestExecution;
using TestMap.Services.TestExecution.Collection;
using TestMap.Services.TestExecution.Mapping;

namespace TestMap.UnitTests.TestExecution;

public sealed class BuildTestResultCollectorTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAndMapAsync_CoverageFailurePersistsWithoutChangingTestOutcome()
    {
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        await File.WriteAllTextAsync(
            Path.Combine(coverageDirectory, "results.trx"),
            """
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testName="PassingTest" outcome="Passed" duration="00:00:00.100" />
              </Results>
            </TestRun>
            """);
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var context = CreateContext(projectDirectory);
        var collector = CreateCollector(context, db);

        var result = await collector.CollectAndMapAsync("failed-run", "2026-08-27", []);

        Assert.Equal("Passed", Assert.Single(result.TestResults).Outcome);
        Assert.NotNull(result.CoverageReport);
        Assert.Equal("CollectionFailed", result.CoverageReport.CollectionStatus);
        Assert.False(result.CoverageReport.HasUsableCoverage);
        var persisted = await db.CoverageReports.SingleAsync();
        Assert.Equal("failed-run", persisted.RunId);
        Assert.Equal("CollectionFailed", persisted.CollectionStatus);
        Assert.Null(persisted.TestRunId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LinkCoverageReportAsync_UsableReportLinksToPersistedTestRun()
    {
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        await WriteSuccessfulCoverageAsync(coverageDirectory, "usable-run");
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedCoveredMemberAsync(db);
        var context = CreateContext(projectDirectory);
        var collector = CreateCollector(context, db);

        var result = await collector.CollectAndMapAsync("usable-run", "2026-08-27", []);
        Assert.NotNull(result.CoverageReport);
        Assert.True(result.CoverageReport.HasUsableCoverage);
        Assert.Equal("Mapped", result.CoverageReport.CollectionStatus);
        var testRun = new TestRunEntity
        {
            ProjectId = context.Project.DbId,
            RunId = "usable-run",
            RunDate = "2026-08-27"
        };
        db.TestRuns.Add(testRun);
        await db.SaveChangesAsync();

        await collector.LinkCoverageReportAsync(testRun.Id, result.CoverageReport);

        var persisted = await db.CoverageReports.SingleAsync();
        Assert.Equal(testRun.Id, persisted.TestRunId);
        Assert.Equal("usable-run", persisted.RunId);
    }

    public void Dispose()
    {
        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static BuildTestResultCollector CreateCollector(ProjectContext context, TestMapDbContext db)
    {
        var mapCoverage = new MapCoverageService(
            context,
            db,
            new CoverageReportRepository(db),
            new ObjectRepository(db),
            new MemberRepository(db),
            new FileRepository(db));
        return new BuildTestResultCollector(
            context,
            new CollectCoverageResultsService(context),
            new CollectMutationTestingResultsService(context),
            new CollectTestResultsService(context, db),
            mapCoverage,
            new MapMutationService(context, new MutationTestingReportRepository(db)));
    }

    private static ProjectContext CreateContext(string projectDirectory) =>
        new(new ProjectModel(directoryPath: projectDirectory) { DbId = 7 });

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }

    private static async Task SeedCoveredMemberAsync(TestMapDbContext db)
    {
        var file = new FileEntity { FilePath = "C:/fixture/src/Widget.cs" };
        db.Files.Add(file);
        await db.SaveChangesAsync();
        var obj = new ObjectEntity
        {
            FileId = file.Id,
            Namespace = "Fixture.Project",
            Name = "Widget",
            Kind = "class",
            Location = new Location(1, 0, 40, 0)
        };
        db.Objects.Add(obj);
        await db.SaveChangesAsync();
        db.Members.Add(new MemberEntity
        {
            ObjectEntityId = obj.Id,
            Name = "Run",
            Kind = "method",
            FullString = "void Run()",
            Location = new Location(29, 0, 31, 0)
        });
        await db.SaveChangesAsync();
    }

    private static async Task WriteSuccessfulCoverageAsync(string coverageDirectory, string runId)
    {
        await File.WriteAllTextAsync(
            Path.Combine(coverageDirectory, $"collection_{runId}.json"),
            $$"""
            {
              "schemaVersion": "coverage-collection-v1",
              "runId": "{{runId}}",
              "status": "Merged",
              "reason": "",
              "testReturnCode": 0,
              "successfulCollector": "XPlat Code Coverage",
              "providerAttempts": [],
              "merge": { "status": "Merged", "inputArtifacts": [], "duplicateArtifacts": [] }
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(coverageDirectory, $"merged_{runId}.cobertura.xml"),
            """
            <coverage line-rate="1" branch-rate="0" complexity="1" version="1"
                      lines-covered="1" lines-valid="1" branches-covered="0" branches-valid="0">
              <packages><package name="Fixture.Project"><classes>
                <class name="Fixture.Project.Widget" filename="src/Widget.cs" line-rate="1" branch-rate="0" complexity="1">
                  <methods><method name="Run" signature="System.Void()" line-rate="1" branch-rate="0">
                    <lines><line number="30" hits="1" /></lines>
                  </method></methods>
                  <lines><line number="30" hits="1" /></lines>
                </class>
              </classes></package></packages>
            </coverage>
            """);
    }
}
