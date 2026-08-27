using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef;

namespace TestMap.UnitTests.Persistence;

public sealed class CoverageIntegrityMigrationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Migration_PreservesLegacyRowsAndMarksMeasurementsUnavailable()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"coverage_integrity_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<TestMapDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using var db = new TestMapDbContext(options);
            var migrator = db.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260725155814_AddAssertionLineageEvidence");

            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO coverage_reports
                    (id, project_id, test_run_id, line_rate, branch_rate, complexity, version, timestamp,
                     lines_covered, lines_valid, branches_covered, branches_valid, created_at)
                VALUES (101, 7, NULL, 0.5, 0.25, 2, 'legacy', 1234, 5, 10, 1, 4, NULL);
                INSERT INTO object_coverages
                    (id, object_id, coverage_report_id, line_rate, branch_rate, lines_covered, lines_valid,
                     branches_covered, branches_valid, complexity)
                VALUES (201, 11, 101, 0.5, 0.25, 5, 10, 1, 4, 2);
                INSERT INTO member_coverages
                    (id, member_id, coverage_report_id, line_rate, branch_rate, lines_covered, lines_valid,
                     branches_covered, branches_valid, complexity)
                VALUES (301, 21, 101, 0.5, 0.25, 5, 10, 1, 4, 2);
                """);

            await migrator.MigrateAsync();

            var report = await db.CoverageReports.SingleAsync(x => x.Id == 101);
            var objectCoverage = await db.ObjectCoverages.SingleAsync(x => x.Id == 201);
            var memberCoverage = await db.MemberCoverages.SingleAsync(x => x.Id == 301);

            Assert.Equal(CoverageReportModel.LegacyCollectionStatus, report.CollectionStatus);
            Assert.Equal(string.Empty, report.MeasurementPolicyVersion);
            Assert.False(report.HasUsableCoverage);
            Assert.False(report.LineCountsAvailable);
            Assert.False(report.BranchCountsAvailable);
            Assert.Equal(11, objectCoverage.ObjectId);
            Assert.Equal(-1, objectCoverage.SourceOrdinal);
            Assert.False(objectCoverage.LineCountsAvailable);
            Assert.Equal(21, memberCoverage.MemberId);
            Assert.Null(memberCoverage.ObjectCoverageId);
            Assert.Equal(-1, memberCoverage.SourceOrdinal);
            Assert.False(memberCoverage.BranchCountsAvailable);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
}
