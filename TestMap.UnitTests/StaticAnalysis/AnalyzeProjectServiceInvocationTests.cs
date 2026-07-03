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
using TestMap.Persistence.Ef.Repositories.Code;
using TestMap.Persistence.Ef.Repositories.Rules;
using TestMap.Services.StaticAnalysis;

namespace TestMap.UnitTests.StaticAnalysis;

/// <summary>
/// Verifies that <see cref="AnalyzeProjectService"/> correctly records cross-project
/// invocations in the Invocations table when a shared member dictionary and topological
/// analysis order are used. Prior to the fix, all cross-project calls were dropped by
/// the <c>IsProjectSymbol</c> filter, leaving SourceTestMappings permanently empty.
/// </summary>
public sealed class AnalyzeProjectServiceInvocationTests
{
    // ─── Test 1: cross-project regular call ──────────────────────────────────

    /// <summary>
    /// A test method that calls a production method in another project produces an
    /// Invocation row whose InvokedMemberId resolves to the production method's DB id.
    /// This verifies the core fix: the IsSolutionSymbol check allows cross-project calls
    /// through, and the shared member dictionary provides the InvokedMemberId.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_CrossProjectCall_ProducesInvocationWithResolvedTargetId()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        var (solution, sourceProjPath, testProjPath) = CreateTwoProjectSolution(
            productionCode: """
                namespace Source;
                public class TargetService
                {
                    public int Compute() => 42;
                }
                """,
            testCode: """
                namespace Source.Tests;
                public class TargetServiceTests
                {
                    [Fact]
                    public void Compute_ReturnsExpectedValue()
                    {
                        var sut = new Source.TargetService();
                        var result = sut.Compute();
                    }
                }
                """);

        var workspace = new InMemoryStaticAnalysisWorkspace(solution);
        var context = CreateProjectContext(sourceProjPath, testProjPath);
        var service = CreateService(db, workspace, context);
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        // Act — production project first (topological order), then test project
        var sourceProject = MakeProjectModel(sourceProjPath, projectReferences: []);
        var testProject = MakeProjectModel(testProjPath, projectReferences: [sourceProjPath]);
        await service.AnalyzeProjectAsync(sourceProject, sharedMemberIds);
        await service.AnalyzeProjectAsync(testProject, sharedMemberIds);

        // Assert — one non-assertion invocation with a resolved cross-project target
        var invocations = await db.Invocations.Where(x => !x.IsAssertion && x.InvokedMemberId != null).ToListAsync();
        var crossProjectInvocation = Assert.Single(invocations);

        var caller = await db.Members.FindAsync(crossProjectInvocation.MemberId);
        Assert.NotNull(caller);
        Assert.True(caller.IsTestMember);
        Assert.Equal("Compute_ReturnsExpectedValue", caller.Name);

        var target = await db.Members.FindAsync(crossProjectInvocation.InvokedMemberId);
        Assert.NotNull(target);
        Assert.False(target.IsTestMember);
        Assert.Equal("Compute", target.Name);
    }

    // ─── Test 2: BCL call is not recorded ────────────────────────────────────

    /// <summary>
    /// A call to a BCL method (e.g. string.Format) inside a test method does not produce
    /// an Invocation row. BCL symbols have no IsInSource location, so IsSolutionSymbol
    /// returns false and the call is correctly ignored.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_BclCallInTestMethod_IsNotRecordedAsInvocation()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        var (solution, sourceProjPath, testProjPath) = CreateTwoProjectSolution(
            productionCode: """
                namespace Source;
                public class TargetService
                {
                    public int Compute() => 42;
                }
                """,
            testCode: """
                namespace Source.Tests;
                public class TargetServiceTests
                {
                    [Fact]
                    public void Compute_UsesStringFormat()
                    {
                        var msg = string.Format("{0}", 42); // BCL — must NOT be recorded
                        var sut = new Source.TargetService();
                        sut.Compute(); // cross-project — must be recorded
                    }
                }
                """);

        var workspace = new InMemoryStaticAnalysisWorkspace(solution);
        var context = CreateProjectContext(sourceProjPath, testProjPath);
        var service = CreateService(db, workspace, context);
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        var sourceProject = MakeProjectModel(sourceProjPath, projectReferences: []);
        var testProject = MakeProjectModel(testProjPath, projectReferences: [sourceProjPath]);

        // Act
        await service.AnalyzeProjectAsync(sourceProject, sharedMemberIds);
        await service.AnalyzeProjectAsync(testProject, sharedMemberIds);

        // Assert — only the cross-project call is present; string.Format is absent
        var invocations = await db.Invocations.Where(x => !x.IsAssertion && x.InvokedMemberId != null).ToListAsync();
        Assert.All(invocations, inv =>
            Assert.DoesNotContain("Format", inv.FullString));
        // The production method call is still recorded
        Assert.Contains(invocations, inv => inv.FullString.Contains("Compute"));
    }

    // ─── Test 3: production→production intra-project call IS persisted ──────────

    /// <summary>
    /// Calls between production methods within the same project produce Invocation rows.
    /// These edges are required by MethodSelectionService BFS strategies (IndirectInvocation,
    /// HelperMediated, ReachableSignalCount) which traverse backward from a production method
    /// through the Invocations table to find covering test methods.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_IntraProjectProductionToProductionCall_IsRecordedAsInvocation()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        // Single-project solution: two production methods where GetTotal calls Compute
        var (solution, sourceProjPath, _) = CreateTwoProjectSolution(
            productionCode: """
                namespace Source;
                public class OrderService
                {
                    public int GetTotal() => Compute();
                    private int Compute() => 42;
                }
                """,
            testCode: ""); // no test project needed for this test

        var workspace = new InMemoryStaticAnalysisWorkspace(solution);
        var context = CreateProjectContext(sourceProjPath, testProjPath: null);
        var service = CreateService(db, workspace, context);
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        var sourceProject = MakeProjectModel(sourceProjPath, projectReferences: []);

        // Act
        await service.AnalyzeProjectAsync(sourceProject, sharedMemberIds);

        // Assert — the production→production edge (GetTotal → Compute) is persisted.
        // MethodSelectionService BFS walks these edges to find test coverage for Compute
        // (test calls GetTotal → GetTotal calls Compute → Compute is covered).
        var invocations = await db.Invocations
            .Where(x => x.InvokedMemberId != null && x.ResolutionStatus == "Resolved")
            .ToListAsync();
        var edge = Assert.Single(invocations);

        var caller = await db.Members.FindAsync(edge.MemberId);
        var target = await db.Members.FindAsync(edge.InvokedMemberId);
        Assert.NotNull(caller);
        Assert.NotNull(target);
        Assert.Equal("GetTotal", caller.Name);
        Assert.Equal("Compute", target.Name);
    }

    // ─── Test 4: cross-project extension method call ──────────────────────────

    /// <summary>
    /// A test method that calls a production method via extension-method syntax produces
    /// an Invocation row whose InvokedMemberId points to the static method on its
    /// declaring class (not to a method on the receiver type). This verifies that
    /// OriginalDefinition unwrapping in ResolveReferencedSymbol handles the reduced form
    /// correctly, and that the shared dictionary lookup succeeds for the static key.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_CrossProjectExtensionMethodCall_InvokedMemberIdPointsToStaticDeclaration()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        var (solution, sourceProjPath, testProjPath) = CreateTwoProjectSolution(
            productionCode: """
                namespace Source;
                public static class StringExtensions
                {
                    public static string Shout(this string value) => value.ToUpper() + "!";
                }
                """,
            testCode: """
                using Source;
                namespace Source.Tests;
                public class StringExtensionsTests
                {
                    [Fact]
                    public void Shout_AppendsExclamation()
                    {
                        var result = "hello".Shout(); // extension-method syntax
                    }
                }
                """);

        var workspace = new InMemoryStaticAnalysisWorkspace(solution);
        var context = CreateProjectContext(sourceProjPath, testProjPath);
        var service = CreateService(db, workspace, context);
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        var sourceProject = MakeProjectModel(sourceProjPath, projectReferences: []);
        var testProject = MakeProjectModel(testProjPath, projectReferences: [sourceProjPath]);

        // Act
        await service.AnalyzeProjectAsync(sourceProject, sharedMemberIds);
        await service.AnalyzeProjectAsync(testProject, sharedMemberIds);

        // Assert — the invocation points to the static Shout method on StringExtensions
        var invocations = await db.Invocations.Where(x => x.InvokedMemberId != null && !x.IsAssertion).ToListAsync();
        var extensionInvocation = Assert.Single(invocations);

        var target = await db.Members.FindAsync(extensionInvocation.InvokedMemberId);
        Assert.NotNull(target);
        Assert.Equal("Shout", target.Name);

        // The declaring object must be StringExtensions (the static class), not string
        var declaringObject = await db.Objects.FindAsync(target.ObjectEntityId);
        Assert.NotNull(declaringObject);
        Assert.Equal("StringExtensions", declaringObject.Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_ObjectCreation_RecordsConstructorInvocation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        var (solution, sourceProjPath, testProjPath) = CreateTwoProjectSolution(
            productionCode: """
                namespace Source;
                public class TargetService
                {
                    public TargetService(int value) { }
                }
                """,
            testCode: """
                namespace Source.Tests;
                public class TargetServiceTests
                {
                    [Fact]
                    public void Constructor_CreatesService()
                    {
                        var sut = new Source.TargetService(42);
                    }
                }
                """);

        var workspace = new InMemoryStaticAnalysisWorkspace(solution);
        var context = CreateProjectContext(sourceProjPath, testProjPath);
        var service = CreateService(db, workspace, context);
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        await service.AnalyzeProjectAsync(MakeProjectModel(sourceProjPath, projectReferences: []), sharedMemberIds);
        await service.AnalyzeProjectAsync(MakeProjectModel(testProjPath, projectReferences: [sourceProjPath]), sharedMemberIds);

        var invocation = await db.Invocations.SingleAsync(x =>
            x.InvokedMemberId.HasValue &&
            x.SyntaxKind == nameof(SyntaxKind.ObjectCreationExpression));
        var target = await db.Members.FindAsync(invocation.InvokedMemberId);

        Assert.NotNull(target);
        Assert.Equal("constructor", target.Kind);
        Assert.Equal("Resolved", invocation.ResolutionStatus);
        Assert.Contains("TargetService", invocation.TargetSymbol);
        Assert.Equal(TestDocPath, invocation.CallerFilePath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_InterfaceDispatch_RecordsImplementationAlias()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        var (solution, sourceProjPath, testProjPath) = CreateTwoProjectSolution(
            productionCode: """
                namespace Source;
                public interface ITargetService
                {
                    int Compute();
                }

                public class TargetService : ITargetService
                {
                    public int Compute() => 42;
                }
                """,
            testCode: """
                namespace Source.Tests;
                public class TargetServiceTests
                {
                    [Fact]
                    public void Compute_ThroughInterface()
                    {
                        Source.ITargetService sut = new Source.TargetService();
                        sut.Compute();
                    }
                }
                """);

        var workspace = new InMemoryStaticAnalysisWorkspace(solution);
        var context = CreateProjectContext(sourceProjPath, testProjPath);
        var service = CreateService(db, workspace, context);
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        await service.AnalyzeProjectAsync(MakeProjectModel(sourceProjPath, projectReferences: []), sharedMemberIds);
        await service.AnalyzeProjectAsync(MakeProjectModel(testProjPath, projectReferences: [sourceProjPath]), sharedMemberIds);

        var resolvedTargets = await (
                from invocation in db.Invocations
                join member in db.Members on invocation.InvokedMemberId equals member.Id
                join sourceObject in db.Objects on member.ObjectEntityId equals sourceObject.Id
                where invocation.FullString.Contains("Compute")
                select new { Member = member, Object = sourceObject })
            .ToListAsync();

        Assert.Contains(resolvedTargets, x =>
            x.Member.Name == "Compute" &&
            x.Object.Name == "ITargetService");
        Assert.Contains(resolvedTargets, x =>
            x.Member.Name == "Compute" &&
            x.Object.Name == "TargetService");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_TargetNotPersisted_PersistsUnresolvedInvocationDiagnostic()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        var (solution, sourceProjPath, testProjPath) = CreateTwoProjectSolution(
            productionCode: """
                namespace Source;
                public class TargetService
                {
                    public int Compute() => 42;
                }
                """,
            testCode: """
                namespace Source.Tests;
                public class TargetServiceTests
                {
                    [Fact]
                    public void Compute_ReturnsExpectedValue()
                    {
                        var sut = new Source.TargetService();
                        sut.Compute();
                    }
                }
                """);

        var workspace = new InMemoryStaticAnalysisWorkspace(solution);
        var context = CreateProjectContext(sourceProjPath, testProjPath);
        var service = CreateService(db, workspace, context);
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        await service.AnalyzeProjectAsync(MakeProjectModel(testProjPath, projectReferences: [sourceProjPath]), sharedMemberIds);

        var unresolved = await db.Invocations.SingleAsync(x =>
            x.ResolutionStatus == "UnresolvedTarget" &&
            x.FullString.Contains("Compute"));

        Assert.Null(unresolved.InvokedMemberId);
        Assert.Equal(nameof(SyntaxKind.InvocationExpression), unresolved.SyntaxKind);
        Assert.Contains("Compute", unresolved.TargetSymbol);
        Assert.Contains("Compute_ReturnsExpectedValue", unresolved.CallerMemberSymbol);
        Assert.Equal(TestDocPath, unresolved.CallerFilePath);
    }

    // ─── Test 5: end-to-end source-test mapping ──────────────────────────────

    /// <summary>
    /// After analyzing a two-project solution in topological order with a shared member
    /// dictionary, running SourceTestMappingRefreshService produces a SourceTestMapping
    /// row linking the production method to the test method. This is the integration
    /// smoke test that proves the full cross-project chain works end-to-end.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_CrossProjectCall_ProducesSourceTestMappingAfterRefresh()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        var (solution, sourceProjPath, testProjPath) = CreateTwoProjectSolution(
            productionCode: """
                namespace Source;
                public class TargetService
                {
                    public int Compute() => 42;
                }
                """,
            testCode: """
                namespace Source.Tests;
                public class TargetServiceTests
                {
                    [Fact]
                    public void Compute_ReturnsExpectedValue()
                    {
                        new Source.TargetService().Compute();
                    }
                }
                """);

        var workspace = new InMemoryStaticAnalysisWorkspace(solution);
        var context = CreateProjectContext(sourceProjPath, testProjPath);
        var service = CreateService(db, workspace, context);
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        var sourceProject = MakeProjectModel(sourceProjPath, projectReferences: []);
        var testProject = MakeProjectModel(testProjPath, projectReferences: [sourceProjPath]);

        // Act — analyze in topological order, then run the BFS
        await service.AnalyzeProjectAsync(sourceProject, sharedMemberIds);
        await service.AnalyzeProjectAsync(testProject, sharedMemberIds);

        // (The service calls RefreshForProjectAsync internally after each project, so
        // mappings are already computed. Verify the final state of the mappings table.)

        // Assert — exactly one mapping: production method ← test method
        var mappings = await db.SourceTestMappings.ToListAsync();
        var mapping = Assert.Single(mappings);

        var sourceMember = await db.Members.FindAsync(mapping.SourceMemberId);
        var testMember = await db.Members.FindAsync(mapping.TestMemberId);
        Assert.NotNull(sourceMember);
        Assert.NotNull(testMember);
        Assert.Equal("Compute", sourceMember.Name);
        Assert.False(sourceMember.IsTestMember);
        Assert.Equal("Compute_ReturnsExpectedValue", testMember.Name);
        Assert.True(testMember.IsTestMember);
        Assert.Equal(1, mapping.PathLength);
        Assert.True(mapping.IsGrounded);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private const string SourceProjPath = "src/Source/Source.csproj";
    private const string TestProjPath = "tests/Source.Tests/Source.Tests.csproj";
    private const string SourceDocPath = "src/Source/TargetService.cs";
    private const string TestDocPath = "tests/Source.Tests/TargetServiceTests.cs";

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task SeedProjectEntityAsync(TestMapDbContext db)
    {
        db.Projects.Add(new ProjectEntity
        {
            Id = 1, Owner = "owner", RepoName = "repo",
            DirectoryPath = ".", ContentHash = "project"
        });
        await db.SaveChangesAsync();
    }

    private static (Solution Solution, string SourceProjPath, string TestProjPath)
        CreateTwoProjectSolution(string productionCode, string testCode)
    {
        var workspace = new AdhocWorkspace();
        var sourceProjId = ProjectId.CreateNewId();
        var testProjId = ProjectId.CreateNewId();

        var bcl = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var solution = workspace.CurrentSolution;

        // Production project
        solution = solution.AddProject(ProjectInfo.Create(
            sourceProjId, VersionStamp.Create(),
            "Source", "Source", LanguageNames.CSharp,
            filePath: SourceProjPath,
            metadataReferences: [bcl],
            parseOptions: parseOptions,
            compilationOptions: compilationOptions));
        if (!string.IsNullOrWhiteSpace(productionCode))
            solution = solution.AddDocument(
                DocumentId.CreateNewId(sourceProjId),
                "TargetService.cs",
                SourceText.From(productionCode),
                filePath: SourceDocPath);

        // Test project — references production
        solution = solution.AddProject(ProjectInfo.Create(
            testProjId, VersionStamp.Create(),
            "Source.Tests", "Source.Tests", LanguageNames.CSharp,
            filePath: TestProjPath,
            metadataReferences: [bcl],
            projectReferences: [new ProjectReference(sourceProjId)],
            parseOptions: parseOptions,
            compilationOptions: compilationOptions));
        if (!string.IsNullOrWhiteSpace(testCode))
            solution = solution.AddDocument(
                DocumentId.CreateNewId(testProjId),
                "TargetServiceTests.cs",
                SourceText.From(testCode),
                filePath: TestDocPath);

        return (solution, SourceProjPath, TestProjPath);
    }

    private static ProjectContext CreateProjectContext(string sourceProjPath, string? testProjPath)
    {
        var config = new TestMapConfig();
        // Configure xUnit framework so [Fact]-annotated methods are recognised as test members
        config.RuntimeConfig.Frameworks = new Dictionary<string, List<string>>
        {
            ["xunit"] = ["Fact", "Theory", "InlineData"]
        };

        var allProjPaths = testProjPath != null
            ? new List<string> { sourceProjPath, testProjPath }
            : new List<string> { sourceProjPath };

        var project = new ProjectModel(config: config)
        {
            DbId = 1,
            Solutions =
            [
                new CSharpSolutionModel(
                    projects: allProjPaths,
                    projectId: 1,
                    filePath: "solution.sln")
            ]
        };
        return new ProjectContext(project);
    }

    private static CSharpProjectModel MakeProjectModel(string filePath, List<string> projectReferences)
    {
        return new CSharpProjectModel(
            projectReferences: projectReferences,
            documentFilePaths: [],
            buildTargets: ["net10.0"],
            buildMetadata: new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" },
            filePath: filePath);
    }

    private static AnalyzeProjectService CreateService(
        TestMapDbContext db,
        IStaticAnalysisWorkspace workspace,
        ProjectContext context)
    {
        var traceService = new RoslynSourceTestTraceService(context, db, workspace);
        var mappingService = new SourceTestMappingRefreshService(context, db, traceService);

        return new AnalyzeProjectService(
            context,
            new CSharpProjectRepository(db),
            new CSharpSolutionRepository(db),
            new FileRepository(db),
            new ObjectRepository(db),
            new MemberRepository(db),
            new ObjectRelationshipRepository(db),
            new MemberRelationshipRepository(db),
            new InvocationRepository(db),
            new RuleAuditRepository(db),
            workspace,
            mappingService);
    }

    // ─── In-memory workspace ─────────────────────────────────────────────────

    private sealed class InMemoryStaticAnalysisWorkspace : IStaticAnalysisWorkspace
    {
        private readonly Solution _solution;

        public InMemoryStaticAnalysisWorkspace(Solution solution) => _solution = solution;

        public IReadOnlyList<string> WorkspaceFailures => [];
        public void ClearWorkspaceFailures() { }

        public Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_solution);

        public Task<Project> OpenProjectAsync(string projectPath, CancellationToken cancellationToken = default)
            => Task.FromResult(FindProject(projectPath));

        public Task<Project> RefreshProjectAsync(string projectPath, CancellationToken cancellationToken = default)
            => OpenProjectAsync(projectPath, cancellationToken);

        private Project FindProject(string projectPath)
            => _solution.Projects.First(p =>
                string.Equals(p.FilePath, projectPath, StringComparison.OrdinalIgnoreCase));
    }
}
