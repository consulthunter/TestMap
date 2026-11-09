using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Coverage;

namespace TestMap.UnitTests.Persistence;

public sealed class ObjectCoverageRepositoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_RoundTripsRawIdentityCountersAndNullableAttribution()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var repository = new ObjectCoverageRepository(db);
        var model = new ObjectCoverageModel
        {
            SourceOrdinal = 0,
            PackageName = "Fixture.Project",
            Name = "Fixture.Project.Widget",
            Filename = "src/Widget.cs",
            AttributionStatus = "Pending",
            AttributionReason = "Awaiting source attribution.",
            LineRate = 0.5,
            BranchRate = 0.25,
            LinesCovered = 5,
            LinesValid = 10,
            BranchesCovered = 1,
            BranchesValid = 4,
            LineCountsAvailable = true,
            BranchCountsAvailable = true,
            ComplexityRaw = "3"
        };

        var id = await repository.InsertOrUpdateAsync(model, objectId: null, coverageReportId: 9);
        var inserted = await repository.GetByIdAsync(id);

        Assert.NotNull(inserted);
        Assert.Null(inserted!.ObjectId);
        Assert.Equal(0, inserted.SourceOrdinal);
        Assert.Equal("Fixture.Project.Widget", inserted.Name);
        Assert.Equal(10, inserted.LinesValid);
        Assert.True(inserted.BranchCountsAvailable);

        model.AttributionStatus = "Mapped";
        model.AttributionReason = string.Empty;
        var updatedId = await repository.InsertOrUpdateAsync(model, objectId: 42, coverageReportId: 9);
        var updated = await db.ObjectCoverages.SingleAsync();

        Assert.Equal(id, updatedId);
        Assert.Equal(42, updated.ObjectId);
        Assert.Equal("Mapped", updated.AttributionStatus);

        await repository.InsertOrUpdateAsync(model, objectId: 43, coverageReportId: 9);
        Assert.Equal(43, (await db.ObjectCoverages.SingleAsync()).ObjectId);
    }
}
