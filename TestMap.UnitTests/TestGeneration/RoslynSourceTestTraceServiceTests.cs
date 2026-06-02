using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Services.StaticAnalysis;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.TestGeneration;

public sealed class RoslynSourceTestTraceServiceTests
{
    private const string SourceProjectPath = "src/Source/Source.csproj";
    private const string TestProjectPath = "tests/Source.Tests/Source.Tests.csproj";
    private const string SourceFilePath = "src/Source/Svc.cs";
    private const string TestFilePath = "tests/Source.Tests/SvcTests.cs";

    /// <summary>
    /// After a refresh, only the directly-called production method is mapped to the test method.
    /// The tracer stops at the first production member and does not follow production→production
    /// calls, so internally-called methods (Deep, called by Entry) are not in the mappings table.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshForProjectAsync_WithRoslynTracer_StopsAtFirstProductionMember()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedAsync(db);
        var workspace = new InMemoryStaticAnalysisWorkspace(CreateSolution());
        var context = new ProjectContext(new ProjectModel(config: new TestMapConfig()) { DbId = 1 });
        var tracer = new RoslynSourceTestTraceService(context, db, workspace);
        var refresh = new SourceTestMappingRefreshService(context, db, tracer);

        await refresh.RefreshForProjectAsync(1, 1);

        var mappings = await db.SourceTestMappings
            .Include(x => x.TraceSteps)
            .OrderBy(x => x.SourceMemberId)
            .ToListAsync();

        // Only the directly-invoked Entry method is mapped.
        var mapping = Assert.Single(mappings);
        Assert.Equal(10, mapping.SourceMemberId);
        Assert.Equal(20, mapping.TestMemberId);
        Assert.Equal("DirectMethodInvocation", mapping.EvidenceKind);
        Assert.Equal("roslyn-source-test-trace-v1", mapping.ResolverVersion);
        Assert.True(mapping.IsGrounded);

        // Deep (member 11) is called by Entry internally but is NOT a mapping target —
        // source-test mapping stops at the first production hit.
        Assert.DoesNotContain(mappings, m => m.SourceMemberId == 11);
    }

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task SeedAsync(TestMapDbContext db)
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
                FilePath = SourceProjectPath,
                BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" },
                ContentHash = "source-project"
            },
            new CSharpProjectEntity
            {
                Id = 2,
                SolutionId = 1,
                FilePath = TestProjectPath,
                BuildMetadata = new ProjectBuildMetadataModel { IsTestProject = true, DefaultBuildTarget = "net10.0" },
                ContentHash = "test-project"
            });
        db.Files.AddRange(
            new FileEntity { Id = 1, CSharpProjectId = 1, FilePath = SourceFilePath, ContentHash = "source-file" },
            new FileEntity { Id = 2, CSharpProjectId = 2, FilePath = TestFilePath, ContentHash = "test-file" });
        db.Objects.AddRange(
            new ObjectEntity
            {
                Id = 1,
                FileId = 1,
                Namespace = "Source",
                Name = "Svc",
                Kind = "class",
                FullString = "public class Svc {}",
                ContentHash = "source-object"
            },
            new ObjectEntity
            {
                Id = 2,
                FileId = 2,
                Namespace = "Source.Tests",
                Name = "SvcTests",
                Kind = "class",
                IsTestObject = true,
                FullString = "public class SvcTests {}",
                ContentHash = "test-object"
            });
        db.Members.AddRange(
            new MemberEntity
            {
                Id = 10,
                ObjectEntityId = 1,
                Name = "Entry",
                Kind = "method",
                Modifiers = ["public"],
                FullString = "public int Entry() => Deep();",
                Location = new Location(4, 4, 4, 4),
                ContentHash = "entry"
            },
            new MemberEntity
            {
                Id = 11,
                ObjectEntityId = 1,
                Name = "Deep",
                Kind = "method",
                Modifiers = ["public"],
                FullString = "public int Deep() => 1;",
                Location = new Location(5, 4, 5, 4),
                ContentHash = "deep"
            },
            new MemberEntity
            {
                Id = 20,
                ObjectEntityId = 2,
                Name = "Entry_ReturnsValue",
                Kind = "method",
                IsTestMember = true,
                FullString = "public void Entry_ReturnsValue() { Assert.Equal(1, new Source.Svc().Entry()); }",
                Location = new Location(5, 4, 5, 4),
                ContentHash = "test"
            });
        await db.SaveChangesAsync();
    }

    private static Solution CreateSolution()
    {
        const string source = """
                              namespace Source;

                              public class Svc
                              {
                                  public int Entry() => Deep();
                                  public int Deep() => 1;
                              }
                              """;
        const string test = """
                            namespace Source.Tests;

                            public class SvcTests
                            {
                                [Fact]
                                public void Entry_ReturnsValue()
                                {
                                    Assert.Equal(1, new Source.Svc().Entry());
                                }
                            }
                            """;

        var workspace = new AdhocWorkspace();
        var sourceProjectId = ProjectId.CreateNewId();
        var testProjectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var bcl = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        solution = solution.AddProject(ProjectInfo.Create(
            sourceProjectId,
            VersionStamp.Create(),
            "Source",
            "Source",
            LanguageNames.CSharp,
            filePath: SourceProjectPath,
            metadataReferences: [bcl],
            parseOptions: parseOptions,
            compilationOptions: compilationOptions));
        solution = solution.AddDocument(
            DocumentId.CreateNewId(sourceProjectId),
            "Svc.cs",
            SourceText.From(source),
            filePath: SourceFilePath);

        solution = solution.AddProject(ProjectInfo.Create(
            testProjectId,
            VersionStamp.Create(),
            "Source.Tests",
            "Source.Tests",
            LanguageNames.CSharp,
            filePath: TestProjectPath,
            metadataReferences: [bcl],
            projectReferences: [new ProjectReference(sourceProjectId)],
            parseOptions: parseOptions,
            compilationOptions: compilationOptions));
        solution = solution.AddDocument(
            DocumentId.CreateNewId(testProjectId),
            "SvcTests.cs",
            SourceText.From(test),
            filePath: TestFilePath);

        return solution;
    }

    /// <summary>
    /// A test method that invokes a test-class helper, which in turn calls a production method,
    /// produces a HelperMediatedPath mapping that is still marked grounded (path.Count = 3 ≤ 3).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TraceAsync_HelperMediatedPath_IsGroundedAndReportsCorrectEvidenceKind()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedHelperMediatedAsync(db);
        // source: Work at line 4; test: InvokeWork at line 4 (test file), Test_WorkViaHelper at line 7
        var workspace = new InMemoryStaticAnalysisWorkspace(CreateHelperMediatedSolution());
        var context = new ProjectContext(new ProjectModel(config: new TestMapConfig()) { DbId = 1 });
        var tracer = new RoslynSourceTestTraceService(context, db, workspace);
        var refresh = new SourceTestMappingRefreshService(context, db, tracer);

        await refresh.RefreshForProjectAsync(1, 1);

        var mappings = await db.SourceTestMappings.Include(x => x.TraceSteps).ToListAsync();
        var mapping = Assert.Single(mappings);
        Assert.Equal(10, mapping.SourceMemberId);
        Assert.Equal(20, mapping.TestMemberId);
        Assert.Equal("HelperMediatedPath", mapping.EvidenceKind);
        Assert.True(mapping.IsGrounded);
        Assert.Equal(2, mapping.PathLength);
        Assert.Equal(2, mapping.TraceSteps.Count);
    }

    /// <summary>
    /// A path through two test-support helpers (depth 3 = path.Count 4) is persisted but
    /// marked ungrounded, because path.Count 4 exceeds the IsGrounded threshold of 3.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TraceAsync_TwoHelperHops_MarkedUngrounded()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedTwoHelperHopsAsync(db);
        var workspace = new InMemoryStaticAnalysisWorkspace(CreateTwoHelperHopsSolution());
        var context = new ProjectContext(new ProjectModel(config: new TestMapConfig()) { DbId = 1 });
        var tracer = new RoslynSourceTestTraceService(context, db, workspace);
        var refresh = new SourceTestMappingRefreshService(context, db, tracer);

        await refresh.RefreshForProjectAsync(1, 1);

        var mappings = await db.SourceTestMappings.ToListAsync();
        var mapping = Assert.Single(mappings);
        Assert.Equal(10, mapping.SourceMemberId);
        Assert.Equal(20, mapping.TestMemberId);
        Assert.False(mapping.IsGrounded);
        Assert.Equal(3, mapping.PathLength);
    }

    /// <summary>
    /// When a test method uses new T() syntax, the tracer records the constructor as the
    /// mapped production member and classifies the evidence as DirectConstructorInvocation.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TraceAsync_ConstructorInvocation_EvidenceIsDirectConstructorInvocation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedConstructorInvocationAsync(db);
        var workspace = new InMemoryStaticAnalysisWorkspace(CreateConstructorSolution());
        var context = new ProjectContext(new ProjectModel(config: new TestMapConfig()) { DbId = 1 });
        var tracer = new RoslynSourceTestTraceService(context, db, workspace);
        var refresh = new SourceTestMappingRefreshService(context, db, tracer);

        await refresh.RefreshForProjectAsync(1, 1);

        var mappings = await db.SourceTestMappings.ToListAsync();
        var mapping = Assert.Single(mappings);
        Assert.Equal(10, mapping.SourceMemberId);
        Assert.Equal(20, mapping.TestMemberId);
        Assert.Equal("DirectConstructorInvocation", mapping.EvidenceKind);
        Assert.True(mapping.IsGrounded);
        Assert.Equal(1, mapping.PathLength);
    }

    /// <summary>
    /// When MaxTestCodeDepth is set to 0, helper-mediated paths are not traversed because
    /// the BFS condition path.TestDepth &lt; maxTestDepth is immediately false.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TraceAsync_MaxTestCodeDepthZero_PreventsHelperMediatedPaths()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedHelperMediatedAsync(db);
        var workspace = new InMemoryStaticAnalysisWorkspace(CreateHelperMediatedSolution());
        var config = new TestMapConfig();
        config.TestingConfig.GenerationConfig.TargetSelection.MaxTestCodeDepth = 0;
        var context = new ProjectContext(new ProjectModel(config: config) { DbId = 1 });
        var tracer = new RoslynSourceTestTraceService(context, db, workspace);
        var refresh = new SourceTestMappingRefreshService(context, db, tracer);

        await refresh.RefreshForProjectAsync(1, 1);

        Assert.Empty(await db.SourceTestMappings.ToListAsync());
    }

    /// <summary>
    /// When test-support members form a call cycle (A calls B calls A), the BFS path-member
    /// guard prevents re-visiting already-visited nodes, ensuring the tracer terminates without
    /// producing spurious mappings.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TraceAsync_CyclicalTestSupportHelpers_TerminatesAndProducesNoMappings()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedCyclicalHelpersAsync(db);
        var workspace = new InMemoryStaticAnalysisWorkspace(CreateCyclicalHelpersSolution());
        var context = new ProjectContext(new ProjectModel(config: new TestMapConfig()) { DbId = 1 });
        var tracer = new RoslynSourceTestTraceService(context, db, workspace);
        var refresh = new SourceTestMappingRefreshService(context, db, tracer);

        await refresh.RefreshForProjectAsync(1, 1);

        // Cycle is broken; production Work is never reached from either helper.
        Assert.Empty(await db.SourceTestMappings.ToListAsync());
    }

    /// <summary>
    /// Multiple test methods that each directly call the same production method produce one
    /// mapping per test method; they are not collapsed into a single row.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TraceAsync_MultipleTestMethods_EachProducesItsOwnMapping()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedMultipleTestMethodsAsync(db);
        var workspace = new InMemoryStaticAnalysisWorkspace(CreateMultipleTestMethodsSolution());
        var context = new ProjectContext(new ProjectModel(config: new TestMapConfig()) { DbId = 1 });
        var tracer = new RoslynSourceTestTraceService(context, db, workspace);
        var refresh = new SourceTestMappingRefreshService(context, db, tracer);

        await refresh.RefreshForProjectAsync(1, 1);

        var mappings = await db.SourceTestMappings.OrderBy(x => x.TestMemberId).ToListAsync();
        Assert.Equal(2, mappings.Count);
        Assert.All(mappings, m => Assert.Equal(10, m.SourceMemberId));
        Assert.Contains(mappings, m => m.TestMemberId == 20);
        Assert.Contains(mappings, m => m.TestMemberId == 21);
    }

    /// <summary>
    /// When a test method calls the same production method twice, deduplication ensures
    /// only one mapping row is produced for the (source, test, evidenceKind) triple.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task TraceAsync_DuplicateCallsToSameTarget_DeduplicatedToSingleMapping()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedDuplicateCallsAsync(db);
        var workspace = new InMemoryStaticAnalysisWorkspace(CreateDuplicateCallsSolution());
        var context = new ProjectContext(new ProjectModel(config: new TestMapConfig()) { DbId = 1 });
        var tracer = new RoslynSourceTestTraceService(context, db, workspace);
        var refresh = new SourceTestMappingRefreshService(context, db, tracer);

        await refresh.RefreshForProjectAsync(1, 1);

        var mappings = await db.SourceTestMappings.ToListAsync();
        var mapping = Assert.Single(mappings);
        Assert.Equal(10, mapping.SourceMemberId);
        Assert.Equal(20, mapping.TestMemberId);
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────

    private static Task SeedHelperMediatedAsync(TestMapDbContext db)
    {
        // source: Work at line 4
        // test:   InvokeWork (helper) at line 4, Test_WorkViaHelper at line 7
        return SeedTwoProjectsAsync(db,
            sourceMembers:
            [
                new MemberEntity
                {
                    Id = 10, ObjectEntityId = 1, Name = "Work", Kind = "method",
                    Modifiers = ["public"], FullString = "public int Work() => 1;",
                    Location = new Location(4, 0, 4, 0), ContentHash = "work"
                }
            ],
            testMembers:
            [
                new MemberEntity
                {
                    Id = 20, ObjectEntityId = 2, Name = "Test_WorkViaHelper", Kind = "method",
                    IsTestMember = true,
                    FullString = "public void Test_WorkViaHelper() { InvokeWork(); }",
                    Location = new Location(7, 0, 7, 0), ContentHash = "test"
                },
                new MemberEntity
                {
                    Id = 30, ObjectEntityId = 2, Name = "InvokeWork", Kind = "method",
                    IsTestMember = false,
                    FullString = "private void InvokeWork() { new Source.Svc().Work(); }",
                    Location = new Location(4, 0, 4, 0), ContentHash = "helper"
                }
            ]);
    }

    private static Task SeedTwoHelperHopsAsync(TestMapDbContext db)
    {
        // test: InnerHelper at line 4, OuterHelper at line 6, Test_DeepPath at line 9
        return SeedTwoProjectsAsync(db,
            sourceMembers:
            [
                new MemberEntity
                {
                    Id = 10, ObjectEntityId = 1, Name = "Work", Kind = "method",
                    Modifiers = ["public"], FullString = "public int Work() => 1;",
                    Location = new Location(4, 0, 4, 0), ContentHash = "work"
                }
            ],
            testMembers:
            [
                new MemberEntity
                {
                    Id = 20, ObjectEntityId = 2, Name = "Test_DeepPath", Kind = "method",
                    IsTestMember = true,
                    FullString = "public void Test_DeepPath() { OuterHelper(); }",
                    Location = new Location(9, 0, 9, 0), ContentHash = "test"
                },
                new MemberEntity
                {
                    Id = 30, ObjectEntityId = 2, Name = "InnerHelper", Kind = "method",
                    FullString = "private void InnerHelper() { new Source.Svc().Work(); }",
                    Location = new Location(4, 0, 4, 0), ContentHash = "inner-helper"
                },
                new MemberEntity
                {
                    Id = 40, ObjectEntityId = 2, Name = "OuterHelper", Kind = "method",
                    FullString = "private void OuterHelper() { InnerHelper(); }",
                    Location = new Location(6, 0, 6, 0), ContentHash = "outer-helper"
                }
            ]);
    }

    private static async Task SeedConstructorInvocationAsync(TestMapDbContext db)
    {
        // Widget constructor at line 4 in SourceFilePath; test at line 5 in TestFilePath.
        // Uses Widget as the source class, so the object entry has Name="Widget" and
        // Namespace="Source" to match what MemberSymbolIndex resolves for the constructor.
        db.Projects.Add(new ProjectEntity
        {
            Id = 1, Owner = "owner", RepoName = "repo", DirectoryPath = ".", ContentHash = "project"
        });
        db.CSharpSolutions.Add(new CSharpSolutionEntity
        {
            Id = 1, ProjectId = 1, FilePath = "repo.sln", ContentHash = "solution"
        });
        db.CSharpProjects.AddRange(
            new CSharpProjectEntity
            {
                Id = 1, SolutionId = 1, FilePath = SourceProjectPath,
                BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" },
                ContentHash = "source-project"
            },
            new CSharpProjectEntity
            {
                Id = 2, SolutionId = 1, FilePath = TestProjectPath,
                BuildMetadata = new ProjectBuildMetadataModel { IsTestProject = true, DefaultBuildTarget = "net10.0" },
                ContentHash = "test-project"
            });
        db.Files.AddRange(
            new FileEntity { Id = 1, CSharpProjectId = 1, FilePath = SourceFilePath, ContentHash = "source-file" },
            new FileEntity { Id = 2, CSharpProjectId = 2, FilePath = TestFilePath, ContentHash = "test-file" });
        db.Objects.AddRange(
            new ObjectEntity
            {
                Id = 1, FileId = 1, Namespace = "Source", Name = "Widget", Kind = "class",
                FullString = "public class Widget {}", ContentHash = "source-object"
            },
            new ObjectEntity
            {
                Id = 2, FileId = 2, Namespace = "Source.Tests", Name = "WidgetTests", Kind = "class",
                IsTestObject = true, FullString = "public class WidgetTests {}", ContentHash = "test-object"
            });
        db.Members.AddRange(
            new MemberEntity
            {
                Id = 10, ObjectEntityId = 1, Name = "Widget", Kind = "constructor",
                Modifiers = ["public"], FullString = "public Widget() { }",
                Location = new Location(4, 0, 4, 0), ContentHash = "ctor"
            },
            new MemberEntity
            {
                Id = 20, ObjectEntityId = 2, Name = "Test_ConstructsWidget", Kind = "method",
                IsTestMember = true,
                FullString = "public void Test_ConstructsWidget() { var w = new Source.Widget(); }",
                Location = new Location(5, 0, 5, 0), ContentHash = "test"
            });
        await db.SaveChangesAsync();
    }

    private static Task SeedCyclicalHelpersAsync(TestMapDbContext db)
    {
        // HelperA at line 4, HelperB at line 6, Test_NoCycle at line 9
        // Work still in source so index can find it, but helpers never reach it
        return SeedTwoProjectsAsync(db,
            sourceMembers:
            [
                new MemberEntity
                {
                    Id = 10, ObjectEntityId = 1, Name = "Work", Kind = "method",
                    Modifiers = ["public"], FullString = "public int Work() => 1;",
                    Location = new Location(4, 0, 4, 0), ContentHash = "work"
                }
            ],
            testMembers:
            [
                new MemberEntity
                {
                    Id = 20, ObjectEntityId = 2, Name = "Test_NoCycle", Kind = "method",
                    IsTestMember = true,
                    FullString = "public void Test_NoCycle() { HelperA(); }",
                    Location = new Location(9, 0, 9, 0), ContentHash = "test"
                },
                new MemberEntity
                {
                    Id = 30, ObjectEntityId = 2, Name = "HelperA", Kind = "method",
                    FullString = "private void HelperA() { HelperB(); }",
                    Location = new Location(4, 0, 4, 0), ContentHash = "helper-a"
                },
                new MemberEntity
                {
                    Id = 40, ObjectEntityId = 2, Name = "HelperB", Kind = "method",
                    FullString = "private void HelperB() { HelperA(); }",
                    Location = new Location(6, 0, 6, 0), ContentHash = "helper-b"
                }
            ]);
    }

    private static Task SeedMultipleTestMethodsAsync(TestMapDbContext db)
    {
        // Work at line 4; TestA at line 5, TestB at line 8
        return SeedTwoProjectsAsync(db,
            sourceMembers:
            [
                new MemberEntity
                {
                    Id = 10, ObjectEntityId = 1, Name = "Work", Kind = "method",
                    Modifiers = ["public"], FullString = "public int Work() => 1;",
                    Location = new Location(4, 0, 4, 0), ContentHash = "work"
                }
            ],
            testMembers:
            [
                new MemberEntity
                {
                    Id = 20, ObjectEntityId = 2, Name = "TestA", Kind = "method",
                    IsTestMember = true,
                    FullString = "public void TestA() { new Source.Svc().Work(); }",
                    Location = new Location(5, 0, 5, 0), ContentHash = "test-a"
                },
                new MemberEntity
                {
                    Id = 21, ObjectEntityId = 2, Name = "TestB", Kind = "method",
                    IsTestMember = true,
                    FullString = "public void TestB() { new Source.Svc().Work(); }",
                    Location = new Location(8, 0, 8, 0), ContentHash = "test-b"
                }
            ]);
    }

    private static Task SeedDuplicateCallsAsync(TestMapDbContext db)
    {
        // Work at line 4; test at line 6 calls Work twice
        return SeedTwoProjectsAsync(db,
            sourceMembers:
            [
                new MemberEntity
                {
                    Id = 10, ObjectEntityId = 1, Name = "Work", Kind = "method",
                    Modifiers = ["public"], FullString = "public int Work() => 1;",
                    Location = new Location(4, 0, 4, 0), ContentHash = "work"
                }
            ],
            testMembers:
            [
                new MemberEntity
                {
                    Id = 20, ObjectEntityId = 2, Name = "Test_CallsTwice", Kind = "method",
                    IsTestMember = true,
                    FullString = "public void Test_CallsTwice() { new Source.Svc().Work(); new Source.Svc().Work(); }",
                    Location = new Location(5, 0, 5, 0), ContentHash = "test"
                }
            ]);
    }

    // ─── Shared infrastructure ────────────────────────────────────────────────

    private static async Task SeedTwoProjectsAsync(
        TestMapDbContext db,
        IReadOnlyList<MemberEntity> sourceMembers,
        IReadOnlyList<MemberEntity> testMembers)
    {
        db.Projects.Add(new ProjectEntity
        {
            Id = 1, Owner = "owner", RepoName = "repo", DirectoryPath = ".", ContentHash = "project"
        });
        db.CSharpSolutions.Add(new CSharpSolutionEntity
        {
            Id = 1, ProjectId = 1, FilePath = "repo.sln", ContentHash = "solution"
        });
        db.CSharpProjects.AddRange(
            new CSharpProjectEntity
            {
                Id = 1, SolutionId = 1, FilePath = SourceProjectPath,
                BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" },
                ContentHash = "source-project"
            },
            new CSharpProjectEntity
            {
                Id = 2, SolutionId = 1, FilePath = TestProjectPath,
                BuildMetadata = new ProjectBuildMetadataModel { IsTestProject = true, DefaultBuildTarget = "net10.0" },
                ContentHash = "test-project"
            });
        db.Files.AddRange(
            new FileEntity { Id = 1, CSharpProjectId = 1, FilePath = SourceFilePath, ContentHash = "source-file" },
            new FileEntity { Id = 2, CSharpProjectId = 2, FilePath = TestFilePath, ContentHash = "test-file" });
        db.Objects.AddRange(
            new ObjectEntity
            {
                Id = 1, FileId = 1, Namespace = "Source", Name = "Svc", Kind = "class",
                FullString = "public class Svc {}", ContentHash = "source-object"
            },
            new ObjectEntity
            {
                Id = 2, FileId = 2, Namespace = "Source.Tests", Name = "SvcTests", Kind = "class",
                IsTestObject = true, FullString = "public class SvcTests {}", ContentHash = "test-object"
            });
        db.Members.AddRange(sourceMembers);
        db.Members.AddRange(testMembers);
        await db.SaveChangesAsync();
    }

    private static Solution CreateSolutionFromSources(string sourceCode, string testCode)
    {
        var workspace = new AdhocWorkspace();
        var sourceProjectId = ProjectId.CreateNewId();
        var testProjectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var bcl = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        solution = solution.AddProject(ProjectInfo.Create(
            sourceProjectId, VersionStamp.Create(), "Source", "Source", LanguageNames.CSharp,
            filePath: SourceProjectPath, metadataReferences: [bcl],
            parseOptions: parseOptions, compilationOptions: compilationOptions));
        solution = solution.AddDocument(
            DocumentId.CreateNewId(sourceProjectId), "Svc.cs",
            SourceText.From(sourceCode), filePath: SourceFilePath);

        solution = solution.AddProject(ProjectInfo.Create(
            testProjectId, VersionStamp.Create(), "Source.Tests", "Source.Tests", LanguageNames.CSharp,
            filePath: TestProjectPath, metadataReferences: [bcl],
            projectReferences: [new ProjectReference(sourceProjectId)],
            parseOptions: parseOptions, compilationOptions: compilationOptions));
        solution = solution.AddDocument(
            DocumentId.CreateNewId(testProjectId), "SvcTests.cs",
            SourceText.From(testCode), filePath: TestFilePath);

        return solution;
    }

    // source: Svc with Work() at line 4 — shared across multiple new tests
    private const string SimpleSvcSource = """
                                           namespace Source;

                                           public class Svc
                                           {
                                               public int Work() => 1;
                                           }
                                           """;

    private static Solution CreateHelperMediatedSolution()
    {
        // test: InvokeWork (helper) at line 4, Test_WorkViaHelper at line 7
        const string test = """
                            namespace Source.Tests;

                            public class SvcTests
                            {
                                private void InvokeWork() { new Source.Svc().Work(); }

                                [Fact]
                                public void Test_WorkViaHelper() { InvokeWork(); }
                            }
                            """;
        return CreateSolutionFromSources(SimpleSvcSource, test);
    }

    private static Solution CreateTwoHelperHopsSolution()
    {
        // test: InnerHelper at line 4, OuterHelper at line 6, Test_DeepPath at line 9
        const string test = """
                            namespace Source.Tests;

                            public class SvcTests
                            {
                                private void InnerHelper() { new Source.Svc().Work(); }

                                private void OuterHelper() { InnerHelper(); }

                                [Fact]
                                public void Test_DeepPath() { OuterHelper(); }
                            }
                            """;
        return CreateSolutionFromSources(SimpleSvcSource, test);
    }

    private static Solution CreateConstructorSolution()
    {
        // source: Widget() at line 4; test: Test_ConstructsWidget at line 5
        const string source = """
                              namespace Source;

                              public class Widget
                              {
                                  public Widget() { }
                              }
                              """;
        const string test = """
                            namespace Source.Tests;

                            public class WidgetTests
                            {
                                [Fact]
                                public void Test_ConstructsWidget() { var w = new Source.Widget(); }
                            }
                            """;
        // Build a fresh two-project solution using Widget instead of Svc.
        var workspace = new AdhocWorkspace();
        var sourceProjectId = ProjectId.CreateNewId();
        var testProjectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var bcl = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        solution = solution.AddProject(ProjectInfo.Create(
            sourceProjectId, VersionStamp.Create(), "Source", "Source", LanguageNames.CSharp,
            filePath: SourceProjectPath, metadataReferences: [bcl],
            parseOptions: parseOptions, compilationOptions: compilationOptions));
        // Re-use SourceFilePath so that DB member 10's file path is matched.
        solution = solution.AddDocument(
            DocumentId.CreateNewId(sourceProjectId), "Widget.cs",
            SourceText.From(source), filePath: SourceFilePath);

        solution = solution.AddProject(ProjectInfo.Create(
            testProjectId, VersionStamp.Create(), "Source.Tests", "Source.Tests", LanguageNames.CSharp,
            filePath: TestProjectPath, metadataReferences: [bcl],
            projectReferences: [new ProjectReference(sourceProjectId)],
            parseOptions: parseOptions, compilationOptions: compilationOptions));
        solution = solution.AddDocument(
            DocumentId.CreateNewId(testProjectId), "WidgetTests.cs",
            SourceText.From(test), filePath: TestFilePath);

        return solution;
    }

    private static Solution CreateCyclicalHelpersSolution()
    {
        // test: HelperA at line 4, HelperB at line 6, Test_NoCycle at line 9
        const string test = """
                            namespace Source.Tests;

                            public class SvcTests
                            {
                                private void HelperA() { HelperB(); }

                                private void HelperB() { HelperA(); }

                                [Fact]
                                public void Test_NoCycle() { HelperA(); }
                            }
                            """;
        return CreateSolutionFromSources(SimpleSvcSource, test);
    }

    private static Solution CreateMultipleTestMethodsSolution()
    {
        // test: TestA at line 5, TestB at line 8
        const string test = """
                            namespace Source.Tests;

                            public class SvcTests
                            {
                                [Fact]
                                public void TestA() { new Source.Svc().Work(); }

                                [Fact]
                                public void TestB() { new Source.Svc().Work(); }
                            }
                            """;
        return CreateSolutionFromSources(SimpleSvcSource, test);
    }

    private static Solution CreateDuplicateCallsSolution()
    {
        // test: Test_CallsTwice at line 5, calls Work() twice
        const string test = """
                            namespace Source.Tests;

                            public class SvcTests
                            {
                                [Fact]
                                public void Test_CallsTwice() { new Source.Svc().Work(); new Source.Svc().Work(); }
                            }
                            """;
        return CreateSolutionFromSources(SimpleSvcSource, test);
    }

    private sealed class InMemoryStaticAnalysisWorkspace : IStaticAnalysisWorkspace
    {
        private readonly Solution _solution;

        public InMemoryStaticAnalysisWorkspace(Solution solution) => _solution = solution;

        public IReadOnlyList<string> WorkspaceFailures => [];
        public void ClearWorkspaceFailures() { }
        public Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_solution);
        public Task<Project> OpenProjectAsync(string projectPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_solution.Projects.First(x =>
                string.Equals(x.FilePath, projectPath, StringComparison.OrdinalIgnoreCase)));
        public Task<Project> RefreshProjectAsync(string projectPath, CancellationToken cancellationToken = default)
            => OpenProjectAsync(projectPath, cancellationToken);
    }
}
