using Microsoft.Data.Sqlite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.TargetSelection.Strategies;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.TestGeneration;

public sealed class MethodSelectionServiceContextMappingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_DirectInvocationOnly_MapsDirectlyInvokingTest()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: true);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Equal(20, context.Method.ExistingTestMemberId);
        Assert.Equal("Target_InvokesProductionMethod", context.Method.ExistingTestMethodName);
        Assert.Contains("Context mapping: DirectInvocation", context.Method.TestStateReason);
        Assert.Contains("directly invokes source member", context.Method.TestStateReason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_DirectInvocationOnly_IgnoresHeuristicContextWithoutInvocation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Null(context.Method.ExistingTestMemberId);
        Assert.Null(context.Method.ExistingTestMethodName);
        Assert.Contains("Context mapping: no test context selected.", context.Method.TestStateReason);
        Assert.Equal("TargetServiceTests", context.TestClassName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_DirectInvocationOnly_UsesSymbolFinderFallbackWhenInvocationRowsAreMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        var workspace = CreateWorkspaceWithDirectReference();
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly, workspace);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Equal(20, context.Method.ExistingTestMemberId);
        Assert.Equal("Target_InvokesProductionMethod", context.Method.ExistingTestMethodName);
        Assert.Equal("RoslynSymbolFinder", context.ContextEvidenceKind);
        Assert.Contains("SymbolFinder found a direct reference", context.ContextEvidenceSummary);
        Assert.NotNull(context.Testability);
        var accessPath = Assert.Single(context.Testability.AccessPaths);
        Assert.Equal(TestAccessStrategy.DirectPublicCall, accessPath.Strategy);
        Assert.Equal([10], accessPath.PathMemberIds);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_WhenCoverageIsMissing_RecordsEvidenceUnavailable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.NotNull(context.Testability);
        Assert.Contains(TestEvidenceStatus.EvidenceUnavailable, context.Testability.EvidenceStatuses);
        Assert.DoesNotContain(TestEvidenceStatus.ObservedUntested, context.Testability.EvidenceStatuses);
        Assert.Contains("EvidenceUnavailable", context.AccessPathSummary);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_DirectPrivateInvocation_DoesNotUseReflectionFallback()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: true);
        var target = await db.Members.SingleAsync(x => x.Id == 10);
        target.FullString = "private int Target() => 1;";
        target.Modifiers = ["private"];
        await db.SaveChangesAsync();
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.NotNull(context.Testability);
        Assert.Equal(MemberVisibility.Private, context.Testability.Visibility);
        var accessPath = Assert.Single(context.Testability.AccessPaths);
        Assert.Equal(TestAccessStrategy.NotReasonablyTestable, accessPath.Strategy);
        Assert.False(accessPath.IsLegalFromTest);
        Assert.False(accessPath.RequiresReflection);
        Assert.NotEqual(TestAccessStrategy.ReflectionFallback, accessPath.Strategy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_DirectInvocationOnly_MapsIndirectlyInvokingTest()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        await SeedIndirectInvocationAsync(db);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Equal(20, context.Method.ExistingTestMemberId);
        Assert.Equal("Target_InvokesProductionMethod", context.Method.ExistingTestMethodName);
        Assert.Equal("IndirectInvocation", context.ContextEvidenceKind);
        Assert.Contains("invokes entrypoint 'PublicTarget'", context.ContextEvidenceSummary);
        Assert.Contains("Context mapping: IndirectInvocation", context.Method.TestStateReason);
        Assert.NotNull(context.Testability);
        Assert.Equal(MemberVisibility.Private, context.Testability.Visibility);
        var accessPath = Assert.Single(context.Testability.AccessPaths);
        Assert.Equal(TestAccessStrategy.PublicCallerPath, accessPath.Strategy);
        Assert.Equal([12, 10], accessPath.PathMemberIds);
        Assert.True(accessPath.IsLegalFromTest);
        Assert.False(accessPath.RequiresReflection);
        Assert.Contains("strategy=PublicCallerPath", context.AccessPathSummary);
        Assert.Contains("path=12 -> 10", context.AccessPathSummary);
        Assert.Contains("Mapped example test: Target_InvokesProductionMethod.", context.AccessPathSummary);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_ConfiguredIndirectDepthZero_DoesNotMapIndirectInvocation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        await SeedIndirectInvocationAsync(db);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly, maxIndirectInvocationDepth: 0);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Null(context.Method.ExistingTestMemberId);
        Assert.Equal("None", context.ContextEvidenceKind);
        Assert.Contains("Context mapping: no test context selected.", context.Method.TestStateReason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_IndirectInvocation_AllowsCrossProductionProjectEntrypoint()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        await SeedCrossProductionProjectEntrypointAsync(db);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Equal(20, context.Method.ExistingTestMemberId);
        Assert.Equal("IndirectInvocation", context.ContextEvidenceKind);
        Assert.Contains("invokes entrypoint 'ApiEntrypoint'", context.ContextEvidenceSummary);
        var accessPath = Assert.Single(context.Testability!.AccessPaths);
        Assert.Equal([40, 10], accessPath.PathMemberIds);
        Assert.Equal(40, accessPath.EntrypointMemberId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_ConfiguredIndirectDepthTwo_MapsTwoProductionHops()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        await SeedTwoHopIndirectInvocationAsync(db);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly, maxIndirectInvocationDepth: 2);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Equal("IndirectInvocation", context.ContextEvidenceKind);
        var accessPath = Assert.Single(context.Testability!.AccessPaths);
        Assert.Equal([13, 12, 10], accessPath.PathMemberIds);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetMethodContextAsync_DirectInvocationOnly_MapsHelperMediatedPath()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        await SeedHelperMediatedInvocationAsync(db);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var context = await service.GetMethodContextAsync(10);

        Assert.NotNull(context);
        Assert.Equal(20, context.Method.ExistingTestMemberId);
        Assert.Equal("Target_InvokesProductionMethod", context.Method.ExistingTestMethodName);
        Assert.Equal("HelperMediatedPath", context.ContextEvidenceKind);
        Assert.Contains("invokes helper 'CreateTargetResult'", context.ContextEvidenceSummary);
        Assert.NotNull(context.Testability);
        Assert.Equal(MemberVisibility.Private, context.Testability.Visibility);
        var accessPath = Assert.Single(context.Testability.AccessPaths);
        Assert.Equal(TestAccessStrategy.HelperMediatedPath, accessPath.Strategy);
        Assert.Equal([30, 12, 10], accessPath.PathMemberIds);
        Assert.True(accessPath.IsLegalFromTest);
        Assert.Contains("strategy=HelperMediatedPath", context.AccessPathSummary);
        Assert.Contains("path=30 -> 12 -> 10", context.AccessPathSummary);
        Assert.NotEmpty(context.Testability.SetupBindings);
        Assert.Contains(context.Testability.SetupBindings, x =>
            x.BindingExpression?.Contains("CreateTargetResult", StringComparison.Ordinal) == true);
        Assert.Contains(context.Testability.SetupBindings, x =>
            x.BindingKind == "Field" &&
            x.RequiredType == "TargetService" &&
            x.SourceMemberId == 31);
        Assert.Contains(context.Testability.SetupBindings, x =>
            x.BindingKind == "Property" &&
            x.RequiredType == "TargetService" &&
            x.SourceMemberId == 32);
        Assert.Contains(context.Testability.SetupBindings, x =>
            x.BindingKind == "SetupMethod" &&
            x.NeedKind == "LifecycleSetup" &&
            x.SourceMemberId == 33);
        Assert.Contains(context.Testability.SetupBindings, x =>
            x.BindingKind == "Constructor" &&
            x.NeedKind == "FixtureConstruction" &&
            x.SourceMemberId == 34);
        Assert.Contains(context.Testability.SetupBindings, x =>
            x.BindingKind == "Builder" &&
            x.RequiredType == "TargetService" &&
            x.IsPreferred);
        Assert.Contains(context.Testability.SetupBindings, x =>
            x.BindingKind == "Factory" &&
            x.RequiredType == "TargetService" &&
            x.IsPreferred);
        Assert.Contains(context.Testability.SetupBindings, x =>
            x.BindingKind == "CompatibleReturn" &&
            x.RequiredType == "TargetService" &&
            x.IsPreferred);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectCandidateMethodsAsync_PersistsAllCandidatesAndSelectsGroundedContextAfterEnrichment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedSelectionProjectAsync(db);
        await RefreshSourceTestMappingsAsync(db);
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var selected = await service.SelectCandidateMethodsAsync(new ExperimentConfig
        {
            CandidateLimit = 1,
            MaxCoverageThreshold = 1.0,
            ContextMappingMode = TestContextMappingMode.DirectInvocationOnly
        });

        var candidate = Assert.Single(selected);
        Assert.Equal(11, candidate.MemberId);
        Assert.Equal(21, candidate.ExistingTestMemberId);
        Assert.False(string.IsNullOrWhiteSpace(candidate.TestIntentionsSummary));
        Assert.Contains("SUT:", candidate.TypeConstructionSummary);
        Assert.NotEqual("{}", candidate.CandidateMetadataJson);
        Assert.Equal(2, await db.CandidateInventory.CountAsync());

        var inventoryItem = await db.CandidateInventory.SingleAsync(x => x.SourceMemberId == 11);
        Assert.Equal("DirectMethodInvocation", inventoryItem.ContextEvidenceKind);
        Assert.Equal(candidate.TestIntentionsSummary, inventoryItem.TestIntentionsSummary);
        Assert.Equal(candidate.TypeConstructionSummary, inventoryItem.TypeConstructionSummary);
        Assert.Equal(candidate.CandidateMetadataJson, inventoryItem.CandidateMetadataJson);
        Assert.True(inventoryItem.HasGroundedTestContext);
        Assert.Equal(21, inventoryItem.MappedTestMemberId);
        Assert.Equal("DirectTarget_InvokesProductionMethod", inventoryItem.MappedTestMethodName);
        Assert.Equal("DirectPublicCall", inventoryItem.AccessPathStrategy);
        Assert.Equal("[11]", inventoryItem.AccessPathMemberIdsJson);
        Assert.Contains("DirectMethodInvocation", inventoryItem.TraceSummary);
        Assert.NotNull(inventoryItem.SourceTestMappingId);
        using var trace = JsonDocument.Parse(inventoryItem.TraceJson);
        Assert.Equal("DirectMethodInvocation", trace.RootElement.GetProperty("EvidenceKind").GetString());
        Assert.True(trace.RootElement.GetProperty("IsGrounded").GetBoolean());
        var traceStep = await db.SourceTestMappingTraceSteps.SingleAsync(x =>
            x.SourceTestMappingId == inventoryItem.SourceTestMappingId);
        Assert.Equal(0, traceStep.StepIndex);
        Assert.Equal(21, traceStep.FromMemberId);
        Assert.Equal(11, traceStep.ToMemberId);
        Assert.Equal("calls", traceStep.RelationshipKind);
        Assert.Equal("Roslyn", traceStep.EdgeSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectCandidateMethodsAsync_IncludesNonPublicMethodsWithIndirectTestContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        await SeedIndirectInvocationAsync(db);
        await RefreshSourceTestMappingsAsync(db);
        db.CoverageReports.Add(new CoverageReportEntity
        {
            Id = 1,
            ProjectId = 1,
            LineRate = 0.0,
            BranchRate = 0.0,
            Complexity = 1,
            Version = "test",
            Timestamp = 1,
            LinesValid = 1
        });
        db.MemberCoverages.Add(new MemberCoverageEntity
        {
            Id = 1,
            MemberId = 10,
            CoverageReportId = 1,
            LineRate = 0.0,
            LinesValid = 1,
            Complexity = 1
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var selected = await service.SelectCandidateMethodsAsync(new ExperimentConfig
        {
            CandidateLimit = 1,
            MaxCoverageThreshold = 1.0,
            ContextMappingMode = TestContextMappingMode.DirectInvocationOnly
        });

        var candidate = Assert.Single(selected);
        Assert.Equal(10, candidate.MemberId);
        Assert.Equal(20, candidate.ExistingTestMemberId);
        Assert.Equal(1, await db.CandidateInventory.CountAsync());
        var inventoryItem = await db.CandidateInventory.SingleAsync();
        Assert.Equal("ProductionMethodPath", inventoryItem.ContextEvidenceKind);
        Assert.Equal("[12,10]", inventoryItem.AccessPathMemberIdsJson);
        Assert.NotNull(inventoryItem.SourceTestMappingId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectCandidateMethodsAsync_PersistsSymbolFinderFallbackTrace()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        db.CoverageReports.Add(new CoverageReportEntity
        {
            Id = 1,
            ProjectId = 1,
            LineRate = 0.0,
            BranchRate = 0.0,
            Complexity = 1,
            Version = "test",
            Timestamp = 1,
            LinesValid = 1
        });
        db.MemberCoverages.Add(new MemberCoverageEntity
        {
            Id = 1,
            MemberId = 10,
            CoverageReportId = 1,
            LineRate = 0.0,
            LinesValid = 1,
            Complexity = 1
        });
        await db.SaveChangesAsync();
        var service = CreateService(
            db,
            TestContextMappingMode.DirectInvocationOnly,
            CreateWorkspaceWithDirectReference());

        var selected = await service.SelectCandidateMethodsAsync(new ExperimentConfig
        {
            CandidateLimit = 1,
            MaxCoverageThreshold = 1.0,
            ContextMappingMode = TestContextMappingMode.DirectInvocationOnly
        });

        var candidate = Assert.Single(selected);
        Assert.Equal(10, candidate.MemberId);
        Assert.Equal(20, candidate.ExistingTestMemberId);
        var inventoryItem = await db.CandidateInventory.SingleAsync();
        Assert.Equal("RoslynSymbolFinder", inventoryItem.ContextEvidenceKind);
        Assert.True(inventoryItem.HasGroundedTestContext);
        Assert.Null(inventoryItem.SourceTestMappingId);
        Assert.Equal("[10]", inventoryItem.AccessPathMemberIdsJson);
        Assert.Empty(await db.SourceTestMappingTraceSteps.ToListAsync());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectCandidateMethodsAsync_ExcludesTestClassHelpersFromSourceCandidates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        await SeedHelperMediatedInvocationAsync(db);
        await RefreshSourceTestMappingsAsync(db);
        db.CoverageReports.Add(new CoverageReportEntity
        {
            Id = 1,
            ProjectId = 1,
            LineRate = 0.0,
            BranchRate = 0.0,
            Complexity = 1,
            Version = "test",
            Timestamp = 1,
            LinesValid = 2
        });
        db.MemberCoverages.AddRange(
            new MemberCoverageEntity
            {
                Id = 1,
                MemberId = 10,
                CoverageReportId = 1,
                LineRate = 0.0,
                LinesValid = 1,
                Complexity = 1
            },
            new MemberCoverageEntity
            {
                Id = 2,
                MemberId = 30,
                CoverageReportId = 1,
                LineRate = 0.0,
                LinesValid = 1,
                Complexity = 1
            });
        await db.SaveChangesAsync();
        var service = CreateService(db, TestContextMappingMode.DirectInvocationOnly);

        var selected = await service.SelectCandidateMethodsAsync(new ExperimentConfig
        {
            CandidateLimit = 10,
            MaxCoverageThreshold = 1.0,
            ContextMappingMode = TestContextMappingMode.DirectInvocationOnly
        });

        Assert.DoesNotContain(selected, x => x.MemberId == 30);
        Assert.DoesNotContain(await db.CandidateInventory.ToListAsync(), x => x.SourceMemberId == 30);
        var candidate = Assert.Single(selected);
        Assert.Equal(10, candidate.MemberId);
        var inventoryItem = await db.CandidateInventory.SingleAsync();
        Assert.Equal("HelperMediatedPath", inventoryItem.ContextEvidenceKind);
        Assert.Equal("[30,12,10]", inventoryItem.AccessPathMemberIdsJson);
        // After the BFS fix, the persisted mapping stops at member 12 (first production hit via helper 30).
        Assert.NotNull(inventoryItem.SourceTestMappingId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SelectCandidateMethodsAsync_PersistsWeakHeuristicAsUngroundedTrace()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDatabaseAsync(connection);
        await SeedProjectAsync(db, includeDirectInvocation: false);
        db.CoverageReports.Add(new CoverageReportEntity
        {
            Id = 1,
            ProjectId = 1,
            LineRate = 0.0,
            BranchRate = 0.0,
            Complexity = 1,
            Version = "test",
            Timestamp = 1,
            LinesValid = 1
        });
        db.MemberCoverages.Add(new MemberCoverageEntity
        {
            Id = 1,
            MemberId = 10,
            CoverageReportId = 1,
            LineRate = 0.0,
            LinesValid = 1,
            Complexity = 1
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, TestContextMappingMode.HeuristicWithGroundedPreference);

        var selected = await service.SelectCandidateMethodsAsync(new ExperimentConfig
        {
            CandidateLimit = 1,
            MaxCoverageThreshold = 1.0,
            ContextMappingMode = TestContextMappingMode.HeuristicWithGroundedPreference
        });

        var candidate = Assert.Single(selected);
        Assert.Equal(10, candidate.MemberId);
        Assert.Null(candidate.ExistingTestMemberId);
        var inventoryItem = await db.CandidateInventory.SingleAsync();
        Assert.Equal("WeakHeuristic", inventoryItem.ContextEvidenceKind);
        Assert.False(inventoryItem.HasGroundedTestContext);
        Assert.Null(inventoryItem.MappedTestMemberId);
        Assert.Equal(string.Empty, inventoryItem.MappedTestMethodName);
        Assert.Contains("Ungrounded weak heuristic", inventoryItem.TraceSummary);

        Assert.Null(inventoryItem.SourceTestMappingId);
        Assert.Empty(await db.SourceTestMappingTraceSteps.ToListAsync());
    }

    private static async Task<TestMapDbContext> CreateDatabaseAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task RefreshSourceTestMappingsAsync(TestMapDbContext db)
    {
        var projectContext = new ProjectContext(new ProjectModel { DbId = 1 });
        var service = new SourceTestMappingRefreshService(
            projectContext,
            db,
            new TestTraceService(await BuildTestMappingsAsync(db)));
        await service.RefreshForProjectAsync(1, 1);
    }

    private static async Task<List<SourceTestMappingEntity>> BuildTestMappingsAsync(TestMapDbContext db)
    {
        var members = await (
                from member in db.Members
                join sourceObject in db.Objects on member.ObjectEntityId equals sourceObject.Id
                join sourceFile in db.Files on sourceObject.FileId equals sourceFile.Id
                join sourceProject in db.CSharpProjects on sourceFile.CSharpProjectId equals sourceProject.Id
                select new
                {
                    member.Id,
                    member.Name,
                    member.IsTestMember,
                    member.Kind,
                    sourceObject.IsTestObject,
                    sourceProject.BuildMetadata.IsTestProject
                })
            .ToDictionaryAsync(x => x.Id);
        var productionIds = members.Values
            .Where(x => x.Kind == "method" && !x.IsTestMember && !x.IsTestObject && !x.IsTestProject)
            .Select(x => x.Id)
            .ToHashSet();
        var edgesByCaller = await db.Invocations
            .Where(x => x.InvokedMemberId.HasValue)
            .GroupBy(x => x.MemberId)
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Select(invocation => invocation.InvokedMemberId!.Value).Distinct().ToList());
        var mappings = new List<SourceTestMappingEntity>();
        foreach (var testMember in members.Values.Where(x => x.IsTestMember && x.Kind == "method"))
        {
            var queue = new Queue<IReadOnlyList<int>>();
            queue.Enqueue([testMember.Id]);
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                if (!edgesByCaller.TryGetValue(path[^1], out var targets)) continue;
                foreach (var target in targets)
                {
                    if (path.Contains(target)) continue;
                    var nextPath = path.Concat([target]).ToList();
                    if (productionIds.Contains(target))
                    {
                        var evidenceKind = nextPath.Count == 2
                            ? "DirectMethodInvocation"
                            : nextPath.Skip(1).SkipLast(1).Any(id => members[id].IsTestObject || members[id].IsTestProject)
                                ? "HelperMediatedPath"
                                : "ProductionMethodPath";
                        mappings.Add(new SourceTestMappingEntity
                        {
                            ProjectId = 1,
                            SourceMemberId = target,
                            TestMemberId = testMember.Id,
                            EvidenceKind = evidenceKind,
                            IsGrounded = true,
                            AccessPathStrategy = nextPath.Count == 2 ? "DirectPublicCall" : "PublicCallerPath",
                            PathLength = nextPath.Count - 1,
                            Confidence = nextPath.Count == 2 ? 1.0 : 0.85,
                            Summary = $"{evidenceKind} test trace.",
                            ResolverVersion = "test-trace",
                            CreatedAt = DateTime.UtcNow,
                            TraceSteps = BuildTestTraceSteps(nextPath)
                        });
                        queue.Enqueue(nextPath);
                    }
                    else
                    {
                        queue.Enqueue(nextPath);
                    }
                }
            }
        }

        return mappings
            .GroupBy(x => new { x.SourceMemberId, x.TestMemberId, x.EvidenceKind })
            .Select(x => x.OrderBy(mapping => mapping.PathLength).First())
            .ToList();
    }

    private static List<SourceTestMappingTraceStepEntity> BuildTestTraceSteps(IReadOnlyList<int> path)
    {
        var steps = new List<SourceTestMappingTraceStepEntity>();
        for (var index = 0; index < path.Count - 1; index++)
            steps.Add(new SourceTestMappingTraceStepEntity
            {
                StepIndex = index,
                FromMemberId = path[index],
                ToMemberId = path[index + 1],
                RelationshipKind = "calls",
                EdgeSource = "Roslyn",
                Summary = $"Trace step {index}: member {path[index]} -> member {path[index + 1]}."
            });

        return steps;
    }

    private static MethodSelectionService CreateService(
        TestMapDbContext db,
        TestContextMappingMode mappingMode,
        IStaticAnalysisWorkspace? workspace = null,
        int? maxIndirectInvocationDepth = null)
    {
        var config = new TestMapConfig();
        config.TestingConfig.GenerationConfig.TargetSelection.ContextMappingMode = mappingMode;
        if (maxIndirectInvocationDepth.HasValue)
            config.TestingConfig.GenerationConfig.TargetSelection.MaxIndirectInvocationDepth =
                maxIndirectInvocationDepth.Value;
        var project = new ProjectModel(config: config)
        {
            DbId = 1,
            Projects =
            [
                new CSharpProjectModel([], [], [])
                {
                    Id = 1,
                    SolutionId = 1,
                    FilePath = "src/Source/Source.csproj",
                    BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" }
                },
                new CSharpProjectModel([], [], [])
                {
                    Id = 2,
                    SolutionId = 1,
                    FilePath = "tests/Source.Tests/Source.Tests.csproj",
                    BuildMetadata = new ProjectBuildMetadataModel
                    {
                        IsTestProject = true,
                        DefaultBuildTarget = "net10.0",
                        Notes = "xunit"
                    }
                }
            ]
        };
        var projectContext = new ProjectContext(project);
        var selector = new CandidateMethodSelector(
            projectContext,
            db,
            config,
            [new ExistingCandidateSelectionStrategy()]);

        return new MethodSelectionService(
            projectContext,
            db,
            selector,
            new CandidateInventoryRepository(db),
            workspace);
    }

    private static IStaticAnalysisWorkspace CreateWorkspaceWithDirectReference()
    {
        const string source = """
                              namespace Source;

                              public class TargetService
                              {
                                  public int Target() => 1;
                              }
                              """;
        const string test = """
                            namespace Source.Tests;

                            public class TargetServiceTests
                            {
                                public void Target_InvokesProductionMethod()
                                {
                                    new Source.TargetService().Target();
                                }
                            }
                            """;

        var workspace = new AdhocWorkspace();
        var sourceProjectId = ProjectId.CreateNewId();
        var testProjectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution;
        solution = solution.AddProject(ProjectInfo.Create(
                sourceProjectId,
                VersionStamp.Create(),
                "Source",
                "Source",
                LanguageNames.CSharp,
                filePath: "src/Source/Source.csproj",
                metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        solution = solution.AddDocument(
            DocumentId.CreateNewId(sourceProjectId),
            "TargetService.cs",
            SourceText.From(source),
            filePath: "src/Source/TargetService.cs");
        solution = solution.AddProject(ProjectInfo.Create(
                testProjectId,
                VersionStamp.Create(),
                "Source.Tests",
                "Source.Tests",
                LanguageNames.CSharp,
                filePath: "tests/Source.Tests/Source.Tests.csproj",
                metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                projectReferences: [new ProjectReference(sourceProjectId)],
                parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        solution = solution.AddDocument(
            DocumentId.CreateNewId(testProjectId),
            "TargetServiceTests.cs",
            SourceText.From(test),
            filePath: "tests/Source.Tests/TargetServiceTests.cs");

        return new InMemoryStaticAnalysisWorkspace(solution);
    }

    private static async Task SeedProjectAsync(TestMapDbContext db, bool includeDirectInvocation)
    {
        db.Projects.Add(new ProjectEntity
        {
            Id = 1,
            Owner = "owner",
            RepoName = "repo",
            DirectoryPath = ".",
            ContentHash = "project"
        });
        db.CSharpSolutions.Add(new CSharpSolutionEntity
        {
            Id = 1,
            ProjectId = 1,
            FilePath = "repo.sln",
            ContentHash = "solution"
        });
        db.CSharpProjects.AddRange(
            new CSharpProjectEntity
            {
                Id = 1,
                SolutionId = 1,
                FilePath = "src/Source/Source.csproj",
                BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" },
                ContentHash = "source-project"
            },
            new CSharpProjectEntity
            {
                Id = 2,
                SolutionId = 1,
                FilePath = "tests/Source.Tests/Source.Tests.csproj",
                BuildMetadata = new ProjectBuildMetadataModel
                {
                    IsTestProject = true,
                    DefaultBuildTarget = "net10.0",
                    Notes = "xunit"
                },
                ContentHash = "test-project"
            });
        db.Files.AddRange(
            new FileEntity
            {
                Id = 1,
                CSharpProjectId = 1,
                FilePath = "src/Source/TargetService.cs",
                ContentHash = "source-file"
            },
            new FileEntity
            {
                Id = 2,
                CSharpProjectId = 2,
                FilePath = "tests/Source.Tests/TargetServiceTests.cs",
                UsingStatements = ["using Xunit;"],
                ContentHash = "test-file"
            });
        db.Objects.AddRange(
            new ObjectEntity
            {
                Id = 1,
                FileId = 1,
                Namespace = "Source",
                Name = "TargetService",
                Kind = "class",
                FullString = "namespace Source; public class TargetService { public int Target() => 1; }",
                ContentHash = "source-object"
            },
            new ObjectEntity
            {
                Id = 2,
                FileId = 2,
                Namespace = "Source.Tests",
                Name = "TargetServiceTests",
                Kind = "class",
                IsTestObject = true,
                TestFramework = "xUnit",
                FullString = "namespace Source.Tests; public class TargetServiceTests { [Fact] public void Target_InvokesProductionMethod() { new Source.TargetService().Target(); } }",
                ContentHash = "test-object"
            });
        db.Members.AddRange(
            new MemberEntity
            {
                Id = 10,
                ObjectEntityId = 1,
                Name = "Target",
                Kind = "method",
                FullString = "public int Target() => 1;",
                Location = new Location(1, 1, 1, 1),
                ContentHash = "source-member"
            },
            new MemberEntity
            {
                Id = 20,
                ObjectEntityId = 2,
                Name = "Target_InvokesProductionMethod",
                Kind = "method",
                IsTestMember = true,
                FullString = "[Fact] public void Target_InvokesProductionMethod() { new Source.TargetService().Target(); }",
                Location = new Location(1, 1, 1, 1),
                ContentHash = "test-member"
            });

        if (includeDirectInvocation)
        {
            db.Invocations.Add(new InvocationEntity
            {
                Id = 1,
                MemberId = 20,
                InvokedMemberId = 10,
                FullString = "Target()",
                ContentHash = "invocation"
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedSelectionProjectAsync(TestMapDbContext db)
    {
        await SeedProjectAsync(db, includeDirectInvocation: false);

        db.Objects.Add(new ObjectEntity
        {
            Id = 3,
            FileId = 1,
            Namespace = "Source",
            Name = "DirectService",
            Kind = "class",
            FullString = "namespace Source; public class DirectService { public int DirectTarget() => 1; }",
            ContentHash = "direct-source-object"
        });
        db.Members.Add(new MemberEntity
        {
            Id = 11,
            ObjectEntityId = 3,
            Name = "DirectTarget",
            Kind = "method",
            FullString = "public int DirectTarget() => 1;",
            Location = new Location(2, 2, 2, 2),
            ContentHash = "direct-source-member"
        });
        db.Members.Add(new MemberEntity
        {
            Id = 21,
            ObjectEntityId = 2,
            Name = "DirectTarget_InvokesProductionMethod",
            Kind = "method",
            IsTestMember = true,
            FullString = "[Fact] public void DirectTarget_InvokesProductionMethod() { new Source.DirectService().DirectTarget(); }",
            Location = new Location(2, 2, 2, 2),
            ContentHash = "direct-test-member"
        });
        db.Invocations.Add(new InvocationEntity
        {
            Id = 2,
            MemberId = 21,
            InvokedMemberId = 11,
            FullString = "DirectTarget()",
            ContentHash = "direct-invocation"
        });
        db.CoverageReports.Add(new CoverageReportEntity
        {
            Id = 1,
            ProjectId = 1,
            LineRate = 0.5,
            BranchRate = 0.0,
            Complexity = 1,
            Version = "test",
            Timestamp = 1,
            LinesCovered = 1,
            LinesValid = 2
        });
        db.MemberCoverages.AddRange(
            new MemberCoverageEntity
            {
                Id = 1,
                MemberId = 10,
                CoverageReportId = 1,
                LineRate = 0.0,
                LinesValid = 1,
                Complexity = 1
            },
            new MemberCoverageEntity
            {
                Id = 2,
                MemberId = 11,
                CoverageReportId = 1,
                LineRate = 0.9,
                LinesCovered = 9,
                LinesValid = 10,
                Complexity = 1
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedIndirectInvocationAsync(TestMapDbContext db)
    {
        var target = await db.Members.SingleAsync(x => x.Id == 10);
        target.FullString = "private int Target() => 1;";
        target.Modifiers = ["private"];

        db.Members.Add(new MemberEntity
        {
            Id = 12,
            ObjectEntityId = 1,
            Name = "PublicTarget",
            Kind = "method",
            Modifiers = ["public"],
            FullString = "public int PublicTarget() => Target();",
            Location = new Location(2, 2, 2, 2),
            ContentHash = "public-entrypoint-member"
        });
        db.Invocations.AddRange(
            new InvocationEntity
            {
                Id = 2,
                MemberId = 12,
                InvokedMemberId = 10,
                FullString = "Target()",
                ContentHash = "source-to-private-invocation"
            },
            new InvocationEntity
            {
                Id = 3,
                MemberId = 20,
                InvokedMemberId = 12,
                FullString = "PublicTarget()",
                ContentHash = "test-to-public-entrypoint-invocation"
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedTwoHopIndirectInvocationAsync(TestMapDbContext db)
    {
        var target = await db.Members.SingleAsync(x => x.Id == 10);
        target.FullString = "private int Target() => 1;";
        target.Modifiers = ["private"];

        db.Members.AddRange(
            new MemberEntity
            {
                Id = 12,
                ObjectEntityId = 1,
                Name = "InnerEntrypoint",
                Kind = "method",
                Modifiers = ["internal"],
                FullString = "internal int InnerEntrypoint() => Target();",
                Location = new Location(2, 2, 2, 2),
                ContentHash = "inner-entrypoint-member"
            },
            new MemberEntity
            {
                Id = 13,
                ObjectEntityId = 1,
                Name = "PublicEntrypoint",
                Kind = "method",
                Modifiers = ["public"],
                FullString = "public int PublicEntrypoint() => InnerEntrypoint();",
                Location = new Location(3, 3, 3, 3),
                ContentHash = "outer-entrypoint-member"
            });
        db.Invocations.AddRange(
            new InvocationEntity
            {
                Id = 2,
                MemberId = 12,
                InvokedMemberId = 10,
                FullString = "Target()",
                ContentHash = "source-to-private-invocation"
            },
            new InvocationEntity
            {
                Id = 3,
                MemberId = 13,
                InvokedMemberId = 12,
                FullString = "InnerEntrypoint()",
                ContentHash = "outer-to-inner-invocation"
            },
            new InvocationEntity
            {
                Id = 4,
                MemberId = 20,
                InvokedMemberId = 13,
                FullString = "PublicEntrypoint()",
                ContentHash = "test-to-public-entrypoint-invocation"
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedCrossProductionProjectEntrypointAsync(TestMapDbContext db)
    {
        var target = await db.Members.SingleAsync(x => x.Id == 10);
        target.FullString = "internal int Target() => 1;";
        target.Modifiers = ["internal"];

        db.CSharpProjects.Add(new CSharpProjectEntity
        {
            Id = 3,
            SolutionId = 1,
            FilePath = "src/Api/Api.csproj",
            BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" },
            ContentHash = "api-project"
        });
        db.Files.Add(new FileEntity
        {
            Id = 3,
            CSharpProjectId = 3,
            FilePath = "src/Api/ApiEntrypoint.cs",
            ContentHash = "api-file"
        });
        db.Objects.Add(new ObjectEntity
        {
            Id = 3,
            FileId = 3,
            Namespace = "Api",
            Name = "ApiEntrypoint",
            Kind = "class",
            FullString = "namespace Api; public class ApiEntrypoint { public int CallCore() => new Source.TargetService().Target(); }",
            ContentHash = "api-object"
        });
        db.Members.Add(new MemberEntity
        {
            Id = 40,
            ObjectEntityId = 3,
            Name = "ApiEntrypoint",
            Kind = "method",
            Modifiers = ["public"],
            FullString = "public int ApiEntrypoint() => new Source.TargetService().Target();",
            Location = new Location(4, 4, 4, 4),
            ContentHash = "api-entrypoint-member"
        });
        db.Invocations.AddRange(
            new InvocationEntity
            {
                Id = 2,
                MemberId = 40,
                InvokedMemberId = 10,
                FullString = "Target()",
                ContentHash = "api-to-core-invocation"
            },
            new InvocationEntity
            {
                Id = 3,
                MemberId = 20,
                InvokedMemberId = 40,
                FullString = "ApiEntrypoint()",
                ContentHash = "test-to-api-invocation"
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedHelperMediatedInvocationAsync(TestMapDbContext db)
    {
        var target = await db.Members.SingleAsync(x => x.Id == 10);
        target.FullString = "private int Target() => 1;";
        target.Modifiers = ["private"];

        db.Members.AddRange(
            new MemberEntity
            {
                Id = 12,
                ObjectEntityId = 1,
                Name = "PublicTarget",
                Kind = "method",
                Modifiers = ["public"],
                FullString = "public int PublicTarget() => Target();",
                Location = new Location(2, 2, 2, 2),
                ContentHash = "public-entrypoint-member"
            },
            new MemberEntity
            {
                Id = 30,
                ObjectEntityId = 2,
                Name = "CreateTargetResult",
                Kind = "method",
                FullString = "private static int CreateTargetResult() { return new Source.TargetService().PublicTarget(); }",
                Location = new Location(3, 3, 3, 3),
                ContentHash = "helper-member"
            },
            new MemberEntity
            {
                Id = 31,
                ObjectEntityId = 2,
                Name = "_sut",
                Kind = "field",
                FullString = "private readonly TargetService _sut;",
                Location = new Location(4, 4, 4, 4),
                ContentHash = "field-member"
            },
            new MemberEntity
            {
                Id = 32,
                ObjectEntityId = 2,
                Name = "Subject",
                Kind = "property",
                FullString = "private TargetService Subject { get; }",
                Location = new Location(5, 5, 5, 5),
                ContentHash = "property-member"
            },
            new MemberEntity
            {
                Id = 33,
                ObjectEntityId = 2,
                Name = "SetUp",
                Kind = "method",
                Attributes = ["SetUp"],
                FullString = "[SetUp] public void SetUp() { }",
                Location = new Location(6, 6, 6, 6),
                ContentHash = "setup-member"
            },
            new MemberEntity
            {
                Id = 34,
                ObjectEntityId = 2,
                Name = "TargetServiceTests",
                Kind = "constructor",
                FullString = "public TargetServiceTests() { }",
                Location = new Location(7, 7, 7, 7),
                ContentHash = "constructor-member"
            },
            new MemberEntity
            {
                Id = 35,
                ObjectEntityId = 2,
                Name = "BuildTargetService",
                Kind = "method",
                FullString = "private TargetService BuildTargetService() => new TargetService();",
                Location = new Location(8, 8, 8, 8),
                ContentHash = "builder-member"
            },
            new MemberEntity
            {
                Id = 36,
                ObjectEntityId = 2,
                Name = "CreateTargetService",
                Kind = "method",
                FullString = "private TargetService CreateTargetService() => new TargetService();",
                Location = new Location(9, 9, 9, 9),
                ContentHash = "factory-member"
            },
            new MemberEntity
            {
                Id = 37,
                ObjectEntityId = 2,
                Name = "SubjectInstance",
                Kind = "method",
                FullString = "private TargetService SubjectInstance() => new TargetService();",
                Location = new Location(10, 10, 10, 10),
                ContentHash = "compatible-return-member"
            });
        db.Invocations.AddRange(
            new InvocationEntity
            {
                Id = 2,
                MemberId = 12,
                InvokedMemberId = 10,
                FullString = "Target()",
                ContentHash = "source-to-private-invocation"
            },
            new InvocationEntity
            {
                Id = 3,
                MemberId = 30,
                InvokedMemberId = 12,
                FullString = "PublicTarget()",
                ContentHash = "helper-to-public-entrypoint-invocation"
            },
            new InvocationEntity
            {
                Id = 4,
                MemberId = 20,
                InvokedMemberId = 30,
                FullString = "CreateTargetResult()",
                ContentHash = "test-to-helper-invocation"
            });

        await db.SaveChangesAsync();
    }

    private sealed class InMemoryStaticAnalysisWorkspace : IStaticAnalysisWorkspace
    {
        private readonly Solution _solution;

        public InMemoryStaticAnalysisWorkspace(Solution solution)
        {
            _solution = solution;
        }

        public IReadOnlyList<string> WorkspaceFailures => [];

        public void ClearWorkspaceFailures()
        {
        }

        public Task<Solution> OpenSolutionAsync(
            string solutionPath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_solution);
        }

        public Task<Project> OpenProjectAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(FindProject(projectPath));
        }

        public Task<Project> RefreshProjectAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            return OpenProjectAsync(projectPath, cancellationToken);
        }

        private Project FindProject(string projectPath)
        {
            return _solution.Projects.First(project =>
                string.Equals(project.FilePath, projectPath, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class TestTraceService : IRoslynSourceTestTraceService
    {
        private readonly List<SourceTestMappingEntity> _mappings;

        public TestTraceService(List<SourceTestMappingEntity> mappings)
        {
            _mappings = mappings;
        }

        public Task<List<SourceTestMappingEntity>?> TraceAsync(
            int projectId,
            int solutionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<List<SourceTestMappingEntity>?>(_mappings);
        }
    }
}
