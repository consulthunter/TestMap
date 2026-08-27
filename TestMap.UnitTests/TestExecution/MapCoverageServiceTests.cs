using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Coverage;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Repositories.Code;
using TestMap.Persistence.Ef.Repositories.Coverage;
using TestMap.Services.TestExecution.Mapping;

namespace TestMap.UnitTests.TestExecution;

public sealed class MapCoverageServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CoverageLinesOverlapMember_ConvertsZeroBasedSourceLineToOneBasedCoverageLine()
    {
        var member = new MemberModel(
            [],
            [],
            [],
            new Location(29, 0, 29, 20),
            name: "Process",
            kind: "method");
        var coverage = new MemberCoverageModel
        {
            Lines = [new LineCoverageModel { Number = 30 }]
        };

        Assert.True(MapCoverageService.CoverageLinesOverlapMember(coverage, member));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CoverageLinesOverlapMember_DoesNotMatchAdjacentPreviousLine()
    {
        var member = new MemberModel(
            [],
            [],
            [],
            new Location(29, 0, 29, 20),
            name: "Process",
            kind: "method");
        var coverage = new MemberCoverageModel
        {
            Lines = [new LineCoverageModel { Number = 29 }]
        };

        Assert.False(MapCoverageService.CoverageLinesOverlapMember(coverage, member));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MapAsync_PersistsEveryRawObservationMapsConstructorsAndReconcilesOnRetry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var seeded = await SeedWidgetAsync(db);
        var service = CreateService(db, projectId: 7);
        var report = CreateCoverageReport("raw-first-run");

        await service.MapAsync(report);
        await service.MapAsync(report);

        var objects = await db.ObjectCoverages.OrderBy(x => x.SourceOrdinal).ToListAsync();
        var members = await db.MemberCoverages.OrderBy(x => x.ObjectCoverageId).ThenBy(x => x.SourceOrdinal).ToListAsync();
        var storedReport = await db.CoverageReports.SingleAsync();

        Assert.Equal(2, objects.Count);
        Assert.Equal(6, members.Count);
        Assert.Equal("Mapped", objects[0].AttributionStatus);
        Assert.Equal(seeded.ObjectId, objects[0].ObjectId);
        Assert.Equal("OutOfProject", objects[1].AttributionStatus);
        Assert.Null(objects[1].ObjectId);
        Assert.All(members, x => Assert.NotNull(x.ObjectCoverageId));

        Assert.Equal(seeded.RunMemberId, members.Single(x => x.Name == "Run").MemberId);
        Assert.Equal(seeded.DefaultConstructorId,
            members.Single(x => x.Name == ".ctor" && x.Signature == "System.Void()").MemberId);
        Assert.Equal(seeded.StringConstructorId,
            members.Single(x => x.Name == ".ctor" && x.Signature.Contains("System.String")).MemberId);
        Assert.Equal(seeded.StaticConstructorId, members.Single(x => x.Name == ".cctor").MemberId);
        Assert.Equal("Unmatched", members.Single(x => x.Name == "Missing").AttributionStatus);
        Assert.Equal("ParentUnmatched", members.Single(x => x.Name == "ExternalMethod").AttributionStatus);

        Assert.Equal(2, storedReport.RawObjectCount);
        Assert.Equal(1, storedReport.MappedObjectCount);
        Assert.Equal(6, storedReport.RawMemberCount);
        Assert.Equal(4, storedReport.MappedMemberCount);
        Assert.Equal("PartiallyMapped", storedReport.CollectionStatus);
        Assert.True(storedReport.HasUsableCoverage);
        Assert.Equal(CoverageReportModel.CorrectedPolicyVersion, storedReport.MeasurementPolicyVersion);

        var gap = await db.CoverageGaps.SingleAsync();
        Assert.Equal(seeded.StringConstructorId, gap.MemberId);
        Assert.Equal(15, gap.LineNumber);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MapAsync_UnresolvedConstructorTie_IsPersistedAsAmbiguousWithoutGap()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var seeded = await SeedWidgetAsync(db);
        db.Members.Add(new MemberEntity
        {
            ObjectEntityId = seeded.ObjectId,
            Name = ".ctor",
            Kind = "constructor",
            FullString = "Widget(string other)",
            Location = new Location(14, 0, 16, 0),
            ContentHash = "constructor-tie"
        });
        await db.SaveChangesAsync();

        var report = new CoverageReportModel
        {
            RunId = "constructor-tie-run",
            Packages =
            [
                new PackageCoverage
                {
                    Name = "Fixture.Project",
                    Classes =
                    [
                        new ObjectCoverageModel
                        {
                            Name = "Fixture.Project.Widget",
                            Filename = "src/Widget.cs",
                            Methods =
                            [
                                new MemberCoverageModel
                                {
                                    Name = ".ctor",
                                    Signature = "System.Void(System.String)",
                                    Lines = [new LineCoverageModel { Number = 15, Hits = 0 }]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        await CreateService(db, projectId: 7).MapAsync(report);

        var member = await db.MemberCoverages.SingleAsync();
        Assert.Equal("Ambiguous", member.AttributionStatus);
        Assert.Null(member.MemberId);
        Assert.Empty(await db.CoverageGaps.ToListAsync());
        Assert.False((await db.CoverageReports.SingleAsync()).HasUsableCoverage);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MapAsync_InterruptedAfterRawSave_LeavesAuditablePendingRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedWidgetAsync(db);
        var report = new CoverageReportModel
        {
            RunId = "interrupted-run",
            Packages =
            [
                new PackageCoverage
                {
                    Name = "Fixture.Project",
                    Classes =
                    [
                        new ObjectCoverageModel
                        {
                            Name = "Fixture.Project.Widget",
                            Filename = "src/Widget.cs",
                            Methods =
                            [
                                new MemberCoverageModel
                                {
                                    Name = "Run",
                                    Signature = "System.Void()",
                                    Lines = [new LineCoverageModel { Number = 30, Hits = 0, Branch = null! }]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        await Assert.ThrowsAsync<NullReferenceException>(() => CreateService(db, 7).MapAsync(report));
        db.ChangeTracker.Clear();

        Assert.Equal("Pending", (await db.ObjectCoverages.SingleAsync()).AttributionStatus);
        Assert.Equal("Pending", (await db.MemberCoverages.SingleAsync()).AttributionStatus);
        Assert.Equal("PendingAttribution", (await db.CoverageReports.SingleAsync()).CollectionStatus);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MapAsync_ClassOutcomes_DistinguishUnsupportedUnmatchedAndAmbiguous()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        var seeded = await SeedWidgetAsync(db);
        db.Files.Add(new FileEntity
        {
            CSharpProjectId = 1,
            FilePath = "C:/fixture/src/Orphan.cs",
            ContentHash = "orphan-file"
        });
        db.Objects.Add(new ObjectEntity
        {
            FileId = (await db.Files.SingleAsync(x => x.ContentHash == "widget-file")).Id,
            Namespace = "Fixture.Project",
            Name = "Widget",
            Kind = "class",
            FullString = "class Widget",
            Location = new Location(0, 0, 50, 0),
            ContentHash = "widget-object-duplicate"
        });
        await db.SaveChangesAsync();
        var report = new CoverageReportModel
        {
            RunId = "class-outcomes-run",
            Packages =
            [
                new PackageCoverage
                {
                    Name = "Fixture.Project",
                    Classes =
                    [
                        new ObjectCoverageModel { Name = string.Empty, Filename = string.Empty },
                        new ObjectCoverageModel { Name = "Fixture.Project.Missing", Filename = "src/Orphan.cs" },
                        new ObjectCoverageModel { Name = "Fixture.Project.Widget", Filename = "src/Widget.cs" }
                    ]
                }
            ]
        };

        await CreateService(db, 7).MapAsync(report);

        var outcomes = await db.ObjectCoverages.OrderBy(x => x.SourceOrdinal)
            .Select(x => x.AttributionStatus)
            .ToListAsync();
        Assert.Equal(["Unsupported", "Unmatched", "Ambiguous"], outcomes);
        Assert.All(await db.ObjectCoverages.ToListAsync(), x => Assert.Null(x.ObjectId));
        Assert.Equal(0, (await db.CoverageReports.SingleAsync()).MappedObjectCount);
        Assert.True(seeded.ObjectId > 0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MapAsync_ImpossibleObjectCounters_ArePersistedAsUnsupported()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDbAsync(connection);
        await SeedWidgetAsync(db);
        var report = CreateCoverageReport("invalid-counter-run");
        var coveredObject = report.Packages.Single().Classes.First();
        coveredObject.Lines =
        [
            new LineCoverageModel
            {
                Number = 30,
                Hits = 1,
                Branch = "true",
                ConditionCoverage = "150% (3/2)"
            }
        ];

        await CreateService(db, 7).MapAsync(report);

        var persistedObject = await db.ObjectCoverages.OrderBy(x => x.SourceOrdinal).FirstAsync();
        Assert.Equal("Unsupported", persistedObject.AttributionStatus);
        Assert.Contains("exceeds", persistedObject.AttributionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(persistedObject.ObjectId);
        Assert.All(
            await db.MemberCoverages.Where(x => x.ObjectCoverageId == persistedObject.Id).ToListAsync(),
            member => Assert.Equal("ParentUnmatched", member.AttributionStatus));
    }

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var db = new TestMapDbContext(
            new DbContextOptionsBuilder<TestMapDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static MapCoverageService CreateService(TestMapDbContext db, int projectId)
    {
        var context = new ProjectContext(new ProjectModel { DbId = projectId });
        return new MapCoverageService(
            context,
            db,
            new CoverageReportRepository(db),
            new ObjectRepository(db),
            new MemberRepository(db),
            new FileRepository(db));
    }

    private static async Task<SeededWidget> SeedWidgetAsync(TestMapDbContext db)
    {
        var file = new FileEntity
        {
            CSharpProjectId = 1,
            FilePath = "C:/fixture/src/Widget.cs",
            ContentHash = "widget-file"
        };
        db.Files.Add(file);
        await db.SaveChangesAsync();
        var widget = new ObjectEntity
        {
            FileId = file.Id,
            Namespace = "Fixture.Project",
            Name = "Widget",
            Kind = "class",
            FullString = "class Widget",
            Location = new Location(0, 0, 50, 0),
            ContentHash = "widget-object"
        };
        db.Objects.Add(widget);
        await db.SaveChangesAsync();

        var run = NewMember(widget.Id, "Run", "method", "void Run()", 29, 31, "run");
        var defaultConstructor = NewMember(widget.Id, ".ctor", "constructor", "Widget()", 9, 11, "ctor-default");
        var stringConstructor = NewMember(widget.Id, ".ctor", "constructor", "Widget(string value)", 14, 16, "ctor-string");
        var staticConstructor = NewMember(widget.Id, ".cctor", "static_constructor", "static Widget()", 4, 5, "cctor");
        db.Members.AddRange(run, defaultConstructor, stringConstructor, staticConstructor);
        await db.SaveChangesAsync();
        return new SeededWidget(widget.Id, run.Id, defaultConstructor.Id, stringConstructor.Id, staticConstructor.Id);
    }

    private static MemberEntity NewMember(
        int objectId,
        string name,
        string kind,
        string fullString,
        int startLine,
        int endLine,
        string hash) => new()
    {
        ObjectEntityId = objectId,
        Name = name,
        Kind = kind,
        FullString = fullString,
        Location = new Location(startLine, 0, endLine, 0),
        ContentHash = hash
    };

    private static CoverageReportModel CreateCoverageReport(string runId) => new()
    {
        RunId = runId,
        Packages =
        [
            new PackageCoverage
            {
                Name = "Fixture.Project",
                Classes =
                [
                    new ObjectCoverageModel
                    {
                        Name = "Fixture.Project.Widget",
                        Filename = "src/Widget.cs",
                        Methods =
                        [
                            NewCoverageMember("Run", "System.Void()", 30, 1),
                            NewCoverageMember(".ctor", "System.Void()", 10, 1),
                            NewCoverageMember(".ctor", "System.Void(System.String)", 15, 0),
                            NewCoverageMember(".cctor", "System.Void()", 5, 1),
                            NewCoverageMember("Missing", "System.Void()", 40, 0)
                        ]
                    },
                    new ObjectCoverageModel
                    {
                        Name = "External.Library.Only",
                        Filename = "external/Only.cs",
                        Methods = [NewCoverageMember("ExternalMethod", "System.Void()", 12, 0)]
                    }
                ]
            }
        ]
    };

    private static MemberCoverageModel NewCoverageMember(
        string name,
        string signature,
        int line,
        int hits) => new()
    {
        Name = name,
        Signature = signature,
        Lines = [new LineCoverageModel { Number = line, Hits = hits }]
    };

    private sealed record SeededWidget(
        int ObjectId,
        int RunMemberId,
        int DefaultConstructorId,
        int StringConstructorId,
        int StaticConstructorId);
}
