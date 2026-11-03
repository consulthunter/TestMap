using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;
using TestMap.Services.StaticAnalysis;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Integration tests for <see cref="SourceTestMappingEntity"/> persistence and
/// <see cref="SourceTestMappingRefreshService"/> upsert logic, using an in-memory SQLite database.
/// </summary>
public sealed class SourceTestMappingPersistenceTests
{
    /// <summary>
    /// All scalar fields on SourceTestMappingEntity are persisted and can be reloaded from the
    /// database with the original values intact.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAndLoad_SourceTestMapping_RoundtripsAllScalarFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        var now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var entity = new SourceTestMappingEntity
        {
            ProjectId = 1,
            SourceMemberId = 100,
            TestMemberId = 200,
            EvidenceKind = "DirectMethodCall",
            IsGrounded = true,
            AccessPathStrategy = "Direct",
            PathLength = 2,
            Confidence = 0.95,
            Summary = "TestA calls SourceB directly.",
            ResolverVersion = "v1",
            CreatedAt = now
        };
        db.SourceTestMappings.Add(entity);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.SourceTestMappings.SingleAsync();
        var domain = loaded.ToDomain();

        Assert.Equal(1, domain.ProjectId);
        Assert.Equal(100, domain.SourceMemberId);
        Assert.Equal(200, domain.TestMemberId);
        Assert.Equal("DirectMethodCall", domain.EvidenceKind);
        Assert.True(domain.IsGrounded);
        Assert.Equal("Direct", domain.AccessPathStrategy);
        Assert.Equal(2, domain.PathLength);
        Assert.Equal(0.95, domain.Confidence);
        Assert.Equal("TestA calls SourceB directly.", domain.Summary);
        Assert.Equal("v1", domain.ResolverVersion);
        Assert.Equal(now, domain.CreatedAt);
    }

    /// <summary>
    /// TraceStep child rows are persisted with correct ordering fields. When reloaded via
    /// Include, ToDomain returns them in StepIndex order with all fields intact.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAndLoad_SourceTestMappingWithTraceSteps_IncludeLoadsStepsInStepIndexOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        var mapping = new SourceTestMappingEntity
        {
            ProjectId = 1,
            SourceMemberId = 100,
            TestMemberId = 200,
            EvidenceKind = "HelperMediatedPath",
            IsGrounded = true,
            AccessPathStrategy = "HelperMediated",
            PathLength = 3,
            Confidence = 0.8,
            Summary = "Via helper.",
            ResolverVersion = "v1",
            CreatedAt = DateTime.UtcNow,
            TraceSteps =
            {
                new SourceTestMappingTraceStepEntity
                {
                    StepIndex = 1,
                    FromMemberId = 300,
                    ToMemberId = 100,
                    RelationshipKind = "ProductionCall",
                    EdgeSource = "roslyn",
                    Summary = "helper → production"
                },
                new SourceTestMappingTraceStepEntity
                {
                    StepIndex = 0,
                    FromMemberId = 200,
                    ToMemberId = 300,
                    RelationshipKind = "TestSupport",
                    EdgeSource = "roslyn",
                    Summary = "test → helper"
                }
            }
        };
        db.SourceTestMappings.Add(mapping);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.SourceTestMappings
            .Include(x => x.TraceSteps)
            .SingleAsync();
        var domain = loaded.ToDomain(); // ToDomain sorts by StepIndex

        Assert.Equal(2, domain.TraceSteps.Count);

        // Steps are returned in StepIndex order (0 then 1)
        Assert.Equal(0, domain.TraceSteps[0].StepIndex);
        Assert.Equal(200, domain.TraceSteps[0].FromMemberId);
        Assert.Equal(300, domain.TraceSteps[0].ToMemberId);
        Assert.Equal("TestSupport", domain.TraceSteps[0].RelationshipKind);
        Assert.Equal("roslyn", domain.TraceSteps[0].EdgeSource);

        Assert.Equal(1, domain.TraceSteps[1].StepIndex);
        Assert.Equal(300, domain.TraceSteps[1].FromMemberId);
        Assert.Equal(100, domain.TraceSteps[1].ToMemberId);
        Assert.Equal("ProductionCall", domain.TraceSteps[1].RelationshipKind);
    }

    /// <summary>
    /// A baseline pass writes the mappings returned by the trace service into an empty table.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_InsertsMappingsFromTraceService()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedTestMembersAsync(db, (200, MemberOrigin.Baseline, null));
        var service = CreateService(db, new FixedTraceService([Mapping(1, 100, 200, "DirectMethodCall")]));

        await service.RefreshAsync(1, [1], new HashSet<int> { 200 }, MemberAnalysisOrigin.Baseline());

        var saved = await db.SourceTestMappings.SingleAsync();
        Assert.Equal(100, saved.SourceMemberId);
        Assert.Equal(200, saved.TestMemberId);
        Assert.Equal("DirectMethodCall", saved.EvidenceKind);
    }

    /// <summary>
    /// A traced test's row that the new trace does not reproduce is removed; rows for tests the
    /// pass did not trace stay, whatever their evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_RemovesOnlyStaleRowsOfTracedTests()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedTestMembersAsync(db, (200, MemberOrigin.Baseline, null), (300, MemberOrigin.ToolAttempt, 9));
        db.SourceTestMappings.AddRange(
            Mapping(1, 50, 200, "OldEvidence"),
            Mapping(1, 50, 300, "OtherAttempt"));
        await db.SaveChangesAsync();
        var service = CreateService(db, new FixedTraceService([Mapping(1, 100, 200, "DirectMethodCall")]));

        await service.RefreshAsync(1, [1], new HashSet<int> { 200 }, MemberAnalysisOrigin.Baseline());

        var rows = await db.SourceTestMappings.AsNoTracking().OrderBy(x => x.TestMemberId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal((100, 200, "DirectMethodCall"), (rows[0].SourceMemberId, rows[0].TestMemberId, rows[0].EvidenceKind));
        Assert.Equal((50, 300, "OtherAttempt"), (rows[1].SourceMemberId, rows[1].TestMemberId, rows[1].EvidenceKind));
    }

    /// <summary>
    /// Refreshing project 1 does not remove mappings belonging to project 2.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_DoesNotRemoveMappingsForOtherProjects()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedTestMembersAsync(db, (200, MemberOrigin.Baseline, null));
        db.SourceTestMappings.Add(Mapping(2, 50, 200, "DirectMethodCall"));
        await db.SaveChangesAsync();
        var service = CreateService(db, new FixedTraceService([Mapping(1, 100, 200, "DirectMethodCall")]));

        await service.RefreshAsync(1, [1], new HashSet<int> { 200 }, MemberAnalysisOrigin.Baseline());

        Assert.Equal(1, await db.SourceTestMappings.CountAsync(x => x.ProjectId == 1));
        Assert.Equal(1, await db.SourceTestMappings.CountAsync(x => x.ProjectId == 2));
    }

    /// <summary>
    /// An attempt pass traces only the observed tests the attempt versioned. A baseline test it
    /// observed unchanged already has its baseline mapping.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_AttemptPass_TracesOnlyNonBaselineTests()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedTestMembersAsync(db,
            (200, MemberOrigin.Baseline, null),
            (201, MemberOrigin.ToolAttempt, 7),
            (202, MemberOrigin.ToolAttempt, 8));
        var tracer = new FixedTraceService([]);
        var service = CreateService(db, tracer);

        await service.RefreshAsync(1, [1], new HashSet<int> { 200, 201 }, MemberAnalysisOrigin.ToolAttempt(7));

        Assert.Equal([201], tracer.TracedTestMemberIds.Order());
        Assert.Equal([200, 201], tracer.IndexMemberIds.Order());
    }

    /// <summary>
    /// A re-traced mapping keeps its id and creation time. Its trace steps are edited in place
    /// by step index, so a changed path never collides on the (mapping, step_index) index.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshAsync_ExistingMapping_UpdatesFieldsAndStepsInPlace()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedTestMembersAsync(db, (200, MemberOrigin.Baseline, null));
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var original = Mapping(1, 100, 200, "HelperMediatedPath", Step(0, 200, 300));
        original.TraceSteps.Add(Step(1, 300, 100));
        original.CreatedAt = created;
        db.SourceTestMappings.Add(original);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var retraced = Mapping(1, 100, 200, "HelperMediatedPath", Step(0, 200, 400));
        retraced.Confidence = 0.42;
        var service = CreateService(db, new FixedTraceService([retraced]));
        await service.RefreshAsync(1, [1], new HashSet<int> { 200 }, MemberAnalysisOrigin.Baseline());

        db.ChangeTracker.Clear();
        var saved = await db.SourceTestMappings.Include(x => x.TraceSteps).SingleAsync();
        Assert.Equal(original.Id, saved.Id);
        Assert.Equal(created, saved.CreatedAt);
        Assert.Equal(0.42, saved.Confidence);
        var step = Assert.Single(saved.TraceSteps);
        Assert.Equal((0, 200, 400), (step.StepIndex, step.FromMemberId, step.ToMemberId));
        Assert.Equal(1, await db.Set<SourceTestMappingTraceStepEntity>().CountAsync());
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

    private static SourceTestMappingRefreshService CreateService(TestMapDbContext db, IRoslynSourceTestTraceService tracer)
    {
        return new SourceTestMappingRefreshService(
            new ProjectContext(new ProjectModel(config: new TestMapConfig())),
            db,
            tracer);
    }

    /// <summary>Seeds one test file holding test-method members with the given origins.</summary>
    private static async Task SeedTestMembersAsync(
        TestMapDbContext db,
        params (int Id, string OriginKind, int? OriginAttemptId)[] members)
    {
        db.Projects.Add(new ProjectEntity { Id = 1, Owner = "owner", RepoName = "repo", DirectoryPath = ".", ContentHash = "p" });
        db.CSharpSolutions.Add(new CSharpSolutionEntity { Id = 1, ProjectId = 1, FilePath = "repo.sln", ContentHash = "s" });
        db.CSharpProjects.Add(new CSharpProjectEntity
        {
            Id = 1,
            SolutionId = 1,
            FilePath = "tests/Tests.csproj",
            BuildMetadata = new ProjectBuildMetadataModel { IsTestProject = true },
            ContentHash = "cp"
        });
        db.Files.Add(new FileEntity { Id = 1, CSharpProjectId = 1, FilePath = "tests/Tests.cs", ContentHash = "f" });
        db.Objects.Add(new ObjectEntity
        {
            Id = 1, FileId = 1, Namespace = "Tests", Name = "Tests", Kind = "class", IsTestObject = true,
            FullString = "public class Tests {}", ContentHash = "o"
        });
        foreach (var (id, originKind, originAttemptId) in members)
            db.Members.Add(new MemberEntity
            {
                Id = id,
                ObjectEntityId = 1,
                Name = $"Test{id}",
                Kind = "method",
                IsTestMember = true,
                FullString = $"public void Test{id}() {{ }}",
                Location = new Location(id, 0, id, 0),
                ContentHash = $"test-{id}",
                OriginKind = originKind,
                OriginAttemptId = originAttemptId
            });
        await db.SaveChangesAsync();
    }

    private static SourceTestMappingEntity Mapping(
        int projectId,
        int sourceMemberId,
        int testMemberId,
        string evidenceKind,
        params SourceTestMappingTraceStepEntity[] steps)
    {
        var mapping = new SourceTestMappingEntity
        {
            ProjectId = projectId,
            SourceMemberId = sourceMemberId,
            TestMemberId = testMemberId,
            EvidenceKind = evidenceKind,
            IsGrounded = true,
            AccessPathStrategy = "Direct",
            PathLength = Math.Max(1, steps.Length),
            Confidence = 1.0,
            Summary = $"{evidenceKind} mapping.",
            ResolverVersion = "v1",
            CreatedAt = DateTime.UtcNow
        };
        foreach (var step in steps) mapping.TraceSteps.Add(step);
        return mapping;
    }

    private static SourceTestMappingTraceStepEntity Step(int index, int from, int to)
    {
        return new SourceTestMappingTraceStepEntity
        {
            StepIndex = index,
            FromMemberId = from,
            ToMemberId = to,
            RelationshipKind = "calls",
            EdgeSource = "Roslyn",
            Summary = $"{from} -> {to}"
        };
    }

    /// <summary>
    /// Test double for <see cref="IRoslynSourceTestTraceService"/> that returns a fixed
    /// pre-set list of <see cref="SourceTestMappingEntity"/> objects and records its scope.
    /// </summary>
    private sealed class FixedTraceService : IRoslynSourceTestTraceService
    {
        private readonly List<SourceTestMappingEntity> _mappings;

        public FixedTraceService(List<SourceTestMappingEntity> mappings) => _mappings = mappings;

        public IReadOnlySet<int> IndexMemberIds { get; private set; } = new HashSet<int>();
        public IReadOnlySet<int> TracedTestMemberIds { get; private set; } = new HashSet<int>();

        public Task<List<SourceTestMappingEntity>?> TraceAsync(
            int projectId,
            int solutionId,
            IReadOnlySet<int> indexMemberIds,
            IReadOnlySet<int> testMemberIds,
            CancellationToken cancellationToken = default)
        {
            IndexMemberIds = indexMemberIds;
            TracedTestMemberIds = testMemberIds;
            return Task.FromResult<List<SourceTestMappingEntity>?>(_mappings);
        }
    }
}
