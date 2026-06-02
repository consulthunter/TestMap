using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Entities.MutationTesting;
using TestMap.Services.RiskScoring;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.RiskScoring;

/// <summary>
/// Integration tests for the database-backed <see cref="IRiskFactorProvider"/>
/// implementations. Each test uses an in-memory SQLite database wrapped in
/// <see cref="TestDatabase"/> so both the <see cref="SqliteConnection"/> and the
/// <see cref="TestMapDbContext"/> are properly disposed after each test.
/// </summary>
public sealed class RiskFactorProviderTests
{
    // ── CoverageGapRiskFactorProvider ─────────────────────────────────────────

    /// <summary>
    /// When no coverage row exists for the candidate member, the score is 0.0 and the
    /// evidence mentions that no data is available.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CoverageGap_NoData_ReturnsZeroWithNoCoverageEvidence()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        var result = await new CoverageGapRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.0, result.Score);
        Assert.Contains("No coverage data", result.Evidence);
    }

    /// <summary>
    /// Full line and branch coverage with zero uncovered gaps produces a score of 0.0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CoverageGap_FullCoverage_ReturnsZeroScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        tdb.Db.MemberCoverages.Add(new MemberCoverageEntity
        {
            MemberId = 1, CoverageReportId = 1,
            LineRate = 1.0, BranchRate = 1.0,
            LinesCovered = 10, LinesValid = 10,
            BranchesCovered = 4, BranchesValid = 4
        });
        await tdb.Db.SaveChangesAsync();

        var result = await new CoverageGapRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.0, result.Score, precision: 3);
    }

    /// <summary>
    /// Zero line and branch coverage with every line as a gap produces the maximum score of 1.0.
    /// lineGap(1.0)×0.60 + branchGap(1.0)×0.25 + gapDensity(1.0)×0.15 = 1.0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CoverageGap_ZeroCoverageAllGaps_ReturnsMaxScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        tdb.Db.MemberCoverages.Add(new MemberCoverageEntity
        {
            MemberId = 1, CoverageReportId = 1,
            LineRate = 0.0, BranchRate = 0.0,
            LinesCovered = 0, LinesValid = 10,
            BranchesCovered = 0, BranchesValid = 0  // no branches → branchGap = lineGap
        });
        for (var i = 1; i <= 10; i++)
            tdb.Db.CoverageGaps.Add(new CoverageGapEntity
            {
                MemberId = 1, CoverageReportId = 1, LineNumber = i, GapKind = "line"
            });
        await tdb.Db.SaveChangesAsync();

        var result = await new CoverageGapRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(1.0, result.Score, precision: 3);
    }

    /// <summary>
    /// 50 % line and branch coverage with 5 of 10 lines as gaps yields a score of 0.5.
    /// lineGap(0.5)×0.60 + branchGap(0.5)×0.25 + gapDensity(0.5)×0.15 = 0.5.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CoverageGap_PartialCoverage_ReturnsProportionalScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        tdb.Db.MemberCoverages.Add(new MemberCoverageEntity
        {
            MemberId = 1, CoverageReportId = 1,
            LineRate = 0.5, BranchRate = 0.5,
            LinesCovered = 5, LinesValid = 10,
            BranchesCovered = 2, BranchesValid = 4
        });
        for (var i = 1; i <= 5; i++)
            tdb.Db.CoverageGaps.Add(new CoverageGapEntity
            {
                MemberId = 1, CoverageReportId = 1, LineNumber = i, GapKind = "line"
            });
        await tdb.Db.SaveChangesAsync();

        var result = await new CoverageGapRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.5, result.Score, precision: 3);
    }

    // ── MutationSurvivalRiskFactorProvider ────────────────────────────────────

    /// <summary>
    /// When no mutants are mapped to the candidate member, the score is 0.0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task MutationSurvival_NoMutants_ReturnsZeroScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        var result = await new MutationSurvivalRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.0, result.Score);
        Assert.Contains("No mapped mutants", result.Evidence);
    }

    /// <summary>
    /// All three mutants survived → every mutant is risky → score = 1.0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task MutationSurvival_AllMutantsSurvived_ReturnsMaxScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        SeedMutants(tdb.Db, memberId: 1, reportId: 1, ["Survived", "Survived", "Survived"]);
        await tdb.Db.SaveChangesAsync();

        var result = await new MutationSurvivalRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(1.0, result.Score, precision: 3);
    }

    /// <summary>
    /// "NoCoverage" mutants are treated as risky (undetected) the same as "Survived" mutants.
    /// 1 NoCoverage + 1 Survived out of 3 total → score = 2/3 ≈ 0.667.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task MutationSurvival_NoCoverageMutantsTreatedAsRisky()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        SeedMutants(tdb.Db, memberId: 1, reportId: 1, ["NoCoverage", "Survived", "Killed"]);
        await tdb.Db.SaveChangesAsync();

        var result = await new MutationSurvivalRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(2.0 / 3.0, result.Score, precision: 3);
    }

    /// <summary>
    /// 3 survived + 2 killed out of 5 total → score = 3/5 = 0.6.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task MutationSurvival_MixedMutants_ReturnsCorrectRatio()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        SeedMutants(tdb.Db, memberId: 1, reportId: 1, ["Survived", "Survived", "Survived", "Killed", "Killed"]);
        await tdb.Db.SaveChangesAsync();

        var result = await new MutationSurvivalRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.6, result.Score, precision: 3);
    }

    // ── ComplexityRiskFactorProvider ──────────────────────────────────────────

    /// <summary>
    /// When no code metric row exists, the provider falls back to source line count.
    /// A 10-line method scores min(1.0, 10/120) ≈ 0.083 and the evidence says "source lines=10".
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Complexity_NoMetricRow_UsesFallbackLineCount()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        // Member spans lines 1–10 → fallbackLines = max(0, 10-1+1) = 10
        var candidate = MakeCandidate(memberId: 1, startLine: 1, endLine: 10);

        var result = await new ComplexityRiskFactorProvider(tdb.Db).ScoreAsync(candidate);

        var expected = Math.Min(1.0, 10.0 / 120.0);
        Assert.Equal(expected, result.Score, precision: 3);
        Assert.Contains("source lines=10", result.Evidence);
    }

    /// <summary>
    /// A method spanning more than 120 lines falls back to a score of 1.0 (clamped).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Complexity_NoMetricRow_FallbackClampedToOneForLongMethod()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        var candidate = MakeCandidate(memberId: 1, startLine: 0, endLine: 240);

        var result = await new ComplexityRiskFactorProvider(tdb.Db).ScoreAsync(candidate);

        Assert.Equal(1.0, result.Score, precision: 3);
    }

    /// <summary>
    /// Maximum inputs (cc=25, coupling=20, sloc=150, MI=50) yield a combined score of 0.925.
    /// complexityScore(1.0)×0.45 + couplingScore(1.0)×0.20 + sizeScore(1.0)×0.20
    ///   + maintRisk(0.5)×0.15 = 0.925.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Complexity_HighMetrics_ReturnsHighScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        tdb.Db.CodeMetrics.Add(new CodeMetricEntity
        {
            EntityId = 1, EntityType = "member",
            CyclomaticComplexity = 25,
            ClassCoupling = 20,
            SourceLinesOfCode = 150,
            MaintainabilityIndex = 50  // maintRisk = 1 − 0.5 = 0.5
        });
        await tdb.Db.SaveChangesAsync();

        var result = await new ComplexityRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.925, result.Score, precision: 3);
    }

    /// <summary>
    /// Zero complexity, coupling, and size with a perfect maintainability index (100)
    /// yields a score of 0.0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Complexity_PerfectMetrics_ReturnsZeroScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        tdb.Db.CodeMetrics.Add(new CodeMetricEntity
        {
            EntityId = 1, EntityType = "member",
            CyclomaticComplexity = 0,
            ClassCoupling = 0,
            SourceLinesOfCode = 0,
            MaintainabilityIndex = 100
        });
        await tdb.Db.SaveChangesAsync();

        var result = await new ComplexityRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.0, result.Score, precision: 3);
    }

    // ── CallGraphRiskFactorProvider ───────────────────────────────────────────

    /// <summary>
    /// No relationships or invocations → fan-in = 0, fan-out = 0 → score = 0.0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CallGraph_NoRelationshipsOrInvocations_ReturnsZeroScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        var result = await new CallGraphRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.0, result.Score);
        Assert.Contains("fan-in=0", result.Evidence);
    }

    /// <summary>
    /// 10 incoming relationships → fanIn=10, fanOut=0.
    /// fanInScore(0.5)×0.65 + fanOutScore(0.0)×0.35 = 0.325.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CallGraph_HighFanIn_ScoresDominantlyFromFanIn()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        for (var i = 1; i <= 10; i++)
            tdb.Db.MemberRelationships.Add(new MemberRelationshipEntity
            {
                SourceId = 100 + i, TargetId = 1, RelationshipType = "calls"
            });
        await tdb.Db.SaveChangesAsync();

        var result = await new CallGraphRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.325, result.Score, precision: 3);
        Assert.Contains("fan-in=10", result.Evidence);
    }

    /// <summary>
    /// 20 incoming relationships + 10 outgoing invocations → fanIn=20, fanOut=10.
    /// fanInScore(1.0)×0.65 + fanOutScore(0.5)×0.35 = 0.825.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CallGraph_FanInAndFanOutBothContributeToScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        for (var i = 1; i <= 20; i++)
            tdb.Db.MemberRelationships.Add(new MemberRelationshipEntity
            {
                SourceId = 200 + i, TargetId = 1, RelationshipType = "calls"
            });
        for (var i = 1; i <= 10; i++)
            tdb.Db.Invocations.Add(new InvocationEntity
            {
                MemberId = 1, InvokedMemberId = 300 + i,
                FullString = $"call{i}()", ContentHash = $"hash-{i}"
            });
        await tdb.Db.SaveChangesAsync();

        var result = await new CallGraphRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.825, result.Score, precision: 3);
    }

    // ── TestGapRiskFactorProvider ─────────────────────────────────────────────

    /// <summary>
    /// No test signals and no coverage data (line rate defaults to 0.0) →
    /// score = max(0.65, 1.0 − 0.0) = 1.0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TestGap_NoTestSignalsNoCoverage_ReturnsMaxScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        var result = await new TestGapRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(1.0, result.Score, precision: 3);
        Assert.Contains("direct test signals=0", result.Evidence);
    }

    /// <summary>
    /// No test signals with 100 % coverage returns the floor of 0.65 — an untested member
    /// is always considered at least moderately risky regardless of coverage.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TestGap_NoTestSignalsFullCoverage_ReturnsCoverageFloor()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        tdb.Db.MemberCoverages.Add(new MemberCoverageEntity
        {
            MemberId = 1, CoverageReportId = 1,
            LineRate = 1.0, LinesValid = 10, LinesCovered = 10
        });
        await tdb.Db.SaveChangesAsync();

        var result = await new TestGapRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.65, result.Score, precision: 3);
    }

    /// <summary>
    /// A "tests" relationship signals direct test coverage; with zero line coverage the
    /// gap score is (1.0 − 0.0) × 0.35 = 0.35.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TestGap_DirectTestRelationshipWithZeroCoverage_ReturnsReducedScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        tdb.Db.MemberRelationships.Add(new MemberRelationshipEntity
        {
            SourceId = 50, TargetId = 1, RelationshipType = "tests"
        });
        // No coverage row → lineRate defaults to 0.0
        await tdb.Db.SaveChangesAsync();

        var result = await new TestGapRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.35, result.Score, precision: 3);
    }

    /// <summary>
    /// A test member that invokes the candidate (via Invocations) with 100 % coverage
    /// yields a score of 0.0 — well-tested and well-covered, no gap.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TestGap_TestMemberInvocationWithFullCoverage_ReturnsZeroScore()
    {
        await using var tdb = await TestDatabase.CreateAsync();
        tdb.Db.Members.Add(new MemberEntity
        {
            Id = 50, ObjectEntityId = 1, Name = "TestMethod", Kind = "method",
            IsTestMember = true, ContentHash = "hash-50"
        });
        tdb.Db.Invocations.Add(new InvocationEntity
        {
            MemberId = 50, InvokedMemberId = 1,
            FullString = "sut.Method()", ContentHash = "inv-hash-1"
        });
        tdb.Db.MemberCoverages.Add(new MemberCoverageEntity
        {
            MemberId = 1, CoverageReportId = 1,
            LineRate = 1.0, LinesValid = 10, LinesCovered = 10
        });
        await tdb.Db.SaveChangesAsync();

        var result = await new TestGapRiskFactorProvider(tdb.Db).ScoreAsync(MakeCandidate(1));

        Assert.Equal(0.0, result.Score, precision: 3);
    }

    // ─── Infrastructure ────────────────────────────────────────────────────────

    private static MemberModel MakeCandidate(
        int memberId,
        int startLine = 1,
        int endLine = 5) =>
        new(
            attributes: [],
            modifiers: ["public"],
            testCategories: [],
            location: new Location(startLine, 0, endLine, 0),
            id: memberId,
            name: $"Method{memberId}",
            kind: "method");

    private static void SeedMutants(
        TestMapDbContext db,
        int memberId,
        int reportId,
        IEnumerable<string> statuses)
    {
        db.MutationTestingReports.Add(new MutationTestingReportEntity
        {
            Id = reportId, ProjectId = 1, SchemaVersion = "test", ProjectRoot = "."
        });
        var n = 1;
        foreach (var status in statuses)
            db.Mutants.Add(new MutantEntity
            {
                MutationTestingReportId = reportId,
                MemberId = memberId,
                StrykerMutantId = $"M{n}",
                Status = status,
                ContentHash = $"hash-{n++}"
            });
    }

    // ─── TestDatabase wrapper ──────────────────────────────────────────────────

    /// <summary>
    /// Manages the lifetime of both the <see cref="SqliteConnection"/> and the
    /// <see cref="TestMapDbContext"/> so the in-memory SQLite database remains alive
    /// for the entire test and is properly cleaned up when the test ends.
    /// </summary>
    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public TestMapDbContext Db { get; }

        private TestDatabase(SqliteConnection connection, TestMapDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<TestMapDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new TestMapDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
