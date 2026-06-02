using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Configuration;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Mapping.Experiment;
using TestMap.Services.StaticAnalysis;

namespace TestMap.UnitTests.Persistence;

/// <summary>
/// Integration tests for <see cref="SourceTestMappingEntity"/> persistence and
/// <see cref="SourceTestMappingRefreshService"/> replace logic, using an in-memory SQLite database.
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
    /// SourceTestMappingRefreshService.RefreshForProjectAsync writes the mappings returned by
    /// the trace service into the database, replacing the empty table with the new rows.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshForProjectAsync_InsertsMappingsFromTraceService()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        var mappings = new List<SourceTestMappingEntity>
        {
            new SourceTestMappingEntity
            {
                ProjectId = 1,
                SourceMemberId = 100,
                TestMemberId = 200,
                EvidenceKind = "DirectMethodCall",
                IsGrounded = true,
                AccessPathStrategy = "Direct",
                PathLength = 2,
                Confidence = 1.0,
                Summary = "Direct call.",
                ResolverVersion = "v1",
                CreatedAt = DateTime.UtcNow
            }
        };

        var service = new SourceTestMappingRefreshService(
            new ProjectContext(new ProjectModel(config: new TestMapConfig())),
            db,
            new FixedTraceService(mappings));

        await service.RefreshForProjectAsync(projectId: 1, solutionId: 1);

        Assert.Equal(1, await db.SourceTestMappings.CountAsync());
        var saved = await db.SourceTestMappings.SingleAsync();
        Assert.Equal(100, saved.SourceMemberId);
        Assert.Equal(200, saved.TestMemberId);
        Assert.Equal("DirectMethodCall", saved.EvidenceKind);
    }

    /// <summary>
    /// A second call to RefreshForProjectAsync for the same project deletes all stale rows that
    /// were written during the first call and inserts the fresh batch from the trace service.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshForProjectAsync_ReplacesStaleRowsWithFreshBatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        // Seed one stale mapping directly
        db.SourceTestMappings.Add(new SourceTestMappingEntity
        {
            ProjectId = 1,
            SourceMemberId = 50,
            TestMemberId = 60,
            EvidenceKind = "OldEvidence",
            IsGrounded = false,
            AccessPathStrategy = "Direct",
            PathLength = 2,
            Confidence = 0.5,
            Summary = "stale",
            ResolverVersion = "v0",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.SourceTestMappings.CountAsync());

        // Refresh replaces stale row with new mapping
        var freshMappings = new List<SourceTestMappingEntity>
        {
            new SourceTestMappingEntity
            {
                ProjectId = 1,
                SourceMemberId = 100,
                TestMemberId = 200,
                EvidenceKind = "DirectMethodCall",
                IsGrounded = true,
                AccessPathStrategy = "Direct",
                PathLength = 2,
                Confidence = 1.0,
                Summary = "Fresh.",
                ResolverVersion = "v1",
                CreatedAt = DateTime.UtcNow
            }
        };

        var service = new SourceTestMappingRefreshService(
            new ProjectContext(new ProjectModel(config: new TestMapConfig())),
            db,
            new FixedTraceService(freshMappings));

        await service.RefreshForProjectAsync(projectId: 1, solutionId: 1);

        Assert.Equal(1, await db.SourceTestMappings.CountAsync());
        var remaining = await db.SourceTestMappings.SingleAsync();
        Assert.Equal(100, remaining.SourceMemberId);
        Assert.Equal("DirectMethodCall", remaining.EvidenceKind);
    }

    /// <summary>
    /// Refreshing project 1 does not remove mappings belonging to project 2.
    /// The replace scope is strictly per-project.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshForProjectAsync_DoesNotRemoveMappingsForOtherProjects()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        // Pre-seed a mapping for project 2
        db.SourceTestMappings.Add(new SourceTestMappingEntity
        {
            ProjectId = 2,
            SourceMemberId = 50,
            TestMemberId = 60,
            EvidenceKind = "DirectMethodCall",
            IsGrounded = true,
            AccessPathStrategy = "Direct",
            PathLength = 2,
            Confidence = 1.0,
            Summary = "Project 2 mapping.",
            ResolverVersion = "v1",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Refresh project 1 (which has one new mapping)
        var project1Mappings = new List<SourceTestMappingEntity>
        {
            new SourceTestMappingEntity
            {
                ProjectId = 1,
                SourceMemberId = 100,
                TestMemberId = 200,
                EvidenceKind = "DirectMethodCall",
                IsGrounded = true,
                AccessPathStrategy = "Direct",
                PathLength = 2,
                Confidence = 1.0,
                Summary = "Project 1 mapping.",
                ResolverVersion = "v1",
                CreatedAt = DateTime.UtcNow
            }
        };

        var service = new SourceTestMappingRefreshService(
            new ProjectContext(new ProjectModel(config: new TestMapConfig())),
            db,
            new FixedTraceService(project1Mappings));

        await service.RefreshForProjectAsync(projectId: 1, solutionId: 1);

        Assert.Equal(1, await db.SourceTestMappings.CountAsync(x => x.ProjectId == 1));
        Assert.Equal(1, await db.SourceTestMappings.CountAsync(x => x.ProjectId == 2));
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

    /// <summary>
    /// Test double for <see cref="IRoslynSourceTestTraceService"/> that returns a fixed
    /// pre-set list of <see cref="SourceTestMappingEntity"/> objects.
    /// </summary>
    private sealed class FixedTraceService : IRoslynSourceTestTraceService
    {
        private readonly List<SourceTestMappingEntity> _mappings;

        public FixedTraceService(List<SourceTestMappingEntity> mappings) => _mappings = mappings;

        public Task<List<SourceTestMappingEntity>?> TraceAsync(
            int projectId,
            int solutionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<List<SourceTestMappingEntity>?>(_mappings);
    }
}
