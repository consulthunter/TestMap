using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Repositories.Coverage;

namespace TestMap.UnitTests.Persistence;

public sealed class MemberCoverageRepositoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InsertOrUpdateAsync_RoundTripsRawIdentityOwnershipAndNullableAttribution()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var parent = new ObjectCoverageEntity
        {
            CoverageReportId = 9,
            SourceOrdinal = 0,
            Name = "Fixture.Project.Widget",
            AttributionStatus = "Pending"
        };
        db.ObjectCoverages.Add(parent);
        await db.SaveChangesAsync();

        var repository = new MemberCoverageRepository(db);
        var model = new MemberCoverageModel
        {
            ObjectCoverageId = parent.Id,
            SourceOrdinal = 2,
            Name = ".ctor",
            Signature = "System.Void(System.String)",
            AttributionStatus = "Pending",
            AttributionReason = "Awaiting source attribution.",
            LineRate = 0,
            LinesCovered = 0,
            LinesValid = 1,
            LineCountsAvailable = true,
            ComplexityRaw = "1"
        };

        var id = await repository.InsertOrUpdateAsync(model, memberId: null, coverageReportId: 9);
        var inserted = await repository.GetByIdAsync(id);

        Assert.NotNull(inserted);
        Assert.Null(inserted!.MemberId);
        Assert.Equal(parent.Id, inserted.ObjectCoverageId);
        Assert.Equal(".ctor", inserted.Name);
        Assert.Equal("System.Void(System.String)", inserted.Signature);

        model.AttributionStatus = "Mapped";
        model.AttributionReason = string.Empty;
        var updatedId = await repository.InsertOrUpdateAsync(model, memberId: 77, coverageReportId: 9);
        var updated = await db.MemberCoverages.SingleAsync();

        Assert.Equal(id, updatedId);
        Assert.Equal(77, updated.MemberId);
        Assert.Equal("Mapped", updated.AttributionStatus);

        await repository.InsertOrUpdateAsync(model, memberId: 78, coverageReportId: 9);
        Assert.Equal(78, (await db.MemberCoverages.SingleAsync()).MemberId);
    }
}
