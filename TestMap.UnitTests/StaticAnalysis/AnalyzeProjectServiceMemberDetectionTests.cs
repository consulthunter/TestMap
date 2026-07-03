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
using CSharpSolutionModel = TestMap.Models.Code.CSharpSolutionModel;

namespace TestMap.UnitTests.StaticAnalysis;

/// <summary>
/// Verifies that <see cref="AnalyzeProjectService"/> correctly detects and classifies
/// test members and test objects for xUnit, NUnit, and MSTest, and that non-test members
/// are not incorrectly flagged as tests.
/// </summary>
public sealed class AnalyzeProjectServiceMemberDetectionTests
{
    /// <summary>
    /// A method annotated with xUnit's [Fact] attribute is persisted with IsTestMember=true,
    /// and its containing class is persisted with IsTestObject=true.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_XUnitFactAttribute_IsTestMemberAndIsTestObjectTrue()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        const string source = """
                              namespace Source.Tests;
                              public class CalculatorTests
                              {
                                  [Fact]
                                  public void Add_ReturnsSum() { }
                              }
                              """;

        const string testProjPath = "tests/Source.Tests/Source.Tests.csproj";
        var workspace = CreateWorkspace(source, "tests/Source.Tests/CalculatorTests.cs",
            testProjPath, isTestProject: true);
        var context = CreateProjectContext(
            new Dictionary<string, List<string>> { ["xunit"] = ["Fact", "Theory"] },
            testProjPath);
        var service = CreateService(db, workspace, context);

        await service.AnalyzeProjectAsync(MakeTestProjectModel(testProjPath), []);

        var member = await db.Members.SingleAsync(x => x.Name == "Add_ReturnsSum");
        Assert.True(member.IsTestMember);
        Assert.Contains("Fact", member.TestCategories);

        var obj = await db.Objects.SingleAsync(x => x.Name == "CalculatorTests");
        Assert.True(obj.IsTestObject);
    }

    /// <summary>
    /// A method annotated with NUnit's [Test] attribute is persisted with IsTestMember=true
    /// when the framework config includes the "Test" attribute name for nunit.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_NUnitTestAttribute_IsTestMemberTrue()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        const string source = """
                              namespace Source.Tests;
                              public class OrderTests
                              {
                                  [Test]
                                  public void Submit_SetsStatus() { }
                              }
                              """;

        const string testProjPath = "tests/Source.Tests/Source.Tests.csproj";
        var workspace = CreateWorkspace(source, "tests/Source.Tests/OrderTests.cs",
            testProjPath, isTestProject: true);
        var context = CreateProjectContext(
            new Dictionary<string, List<string>> { ["nunit"] = ["Test", "TestCase", "TestFixture"] },
            testProjPath);
        var service = CreateService(db, workspace, context);

        await service.AnalyzeProjectAsync(MakeTestProjectModel(testProjPath), []);

        var member = await db.Members.SingleAsync(x => x.Name == "Submit_SetsStatus");
        Assert.True(member.IsTestMember);
        Assert.Contains("Test", member.TestCategories);
    }

    /// <summary>
    /// A method annotated with MSTest's [TestMethod] attribute is persisted with
    /// IsTestMember=true when the framework config includes "TestMethod" for mstest.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_MSTestTestMethodAttribute_IsTestMemberTrue()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        const string source = """
                              namespace Source.Tests;
                              public class UserTests
                              {
                                  [TestMethod]
                                  public void Create_SetsId() { }
                              }
                              """;

        const string testProjPath = "tests/Source.Tests/Source.Tests.csproj";
        var workspace = CreateWorkspace(source, "tests/Source.Tests/UserTests.cs",
            testProjPath, isTestProject: true);
        var context = CreateProjectContext(
            new Dictionary<string, List<string>> { ["mstest"] = ["TestMethod", "DataTestMethod"] },
            testProjPath);
        var service = CreateService(db, workspace, context);

        await service.AnalyzeProjectAsync(MakeTestProjectModel(testProjPath), []);

        var member = await db.Members.SingleAsync(x => x.Name == "Create_SetsId");
        Assert.True(member.IsTestMember);
        Assert.Contains("TestMethod", member.TestCategories);
    }

    /// <summary>
    /// A production method with no test-framework attribute is persisted with IsTestMember=false
    /// and its containing class has IsTestObject=false.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_ProductionMethod_IsTestMemberFalseAndIsTestObjectFalse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        const string source = """
                              namespace Source;
                              public class Calculator
                              {
                                  public int Add(int a, int b) => a + b;
                              }
                              """;

        const string srcProjPath = "src/Source/Source.csproj";
        var workspace = CreateWorkspace(source, "src/Source/Calculator.cs",
            srcProjPath, isTestProject: false);
        var context = CreateProjectContext(
            new Dictionary<string, List<string>> { ["xunit"] = ["Fact", "Theory"] },
            srcProjPath);
        var service = CreateService(db, workspace, context);

        await service.AnalyzeProjectAsync(MakeProductionProjectModel(srcProjPath), []);

        var member = await db.Members.SingleAsync(x => x.Name == "Add");
        Assert.False(member.IsTestMember);
        Assert.Equal("method", member.Kind);

        var obj = await db.Objects.SingleAsync(x => x.Name == "Calculator");
        Assert.False(obj.IsTestObject);
    }

    /// <summary>
    /// A test class helper method (not annotated with a test attribute) is persisted with
    /// IsTestMember=false but its containing class (which has test members) has IsTestObject=true.
    /// This ensures test helpers are correctly classified as TestSupport in the BFS traversal.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_TestHelperInTestClass_IsTestMemberFalseButObjectIsTestObject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);
        await SeedProjectEntityAsync(db);

        const string source = """
                              namespace Source.Tests;
                              public class ServiceTests
                              {
                                  [Fact]
                                  public void Act_DoesWork() { Arrange(); }

                                  private void Arrange() { }
                              }
                              """;

        const string testProjPath = "tests/Source.Tests/Source.Tests.csproj";
        var workspace = CreateWorkspace(source, "tests/Source.Tests/ServiceTests.cs",
            testProjPath, isTestProject: true);
        var context = CreateProjectContext(
            new Dictionary<string, List<string>> { ["xunit"] = ["Fact", "Theory"] },
            testProjPath);
        var service = CreateService(db, workspace, context);

        await service.AnalyzeProjectAsync(MakeTestProjectModel(testProjPath), []);

        var testMethod = await db.Members.SingleAsync(x => x.Name == "Act_DoesWork");
        Assert.True(testMethod.IsTestMember);

        var helperMethod = await db.Members.SingleAsync(x => x.Name == "Arrange");
        Assert.False(helperMethod.IsTestMember);

        // Both members are in the same class, which must be IsTestObject=true because it
        // has at least one test method.
        var obj = await db.Objects.SingleAsync(x => x.Name == "ServiceTests");
        Assert.True(obj.IsTestObject);
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

    private static async Task SeedProjectEntityAsync(TestMapDbContext db)
    {
        db.Projects.Add(new ProjectEntity
        {
            Id = 1, Owner = "owner", RepoName = "repo",
            DirectoryPath = ".", ContentHash = "project"
        });
        await db.SaveChangesAsync();
    }

    private static ProjectContext CreateProjectContext(
        Dictionary<string, List<string>> frameworks,
        params string[] projectFilePaths)
    {
        var config = new TestMapConfig();
        config.RuntimeConfig.Frameworks = frameworks;
        var project = new ProjectModel(config: config)
        {
            DbId = 1,
            Solutions =
            [
                new CSharpSolutionModel(
                    projects: projectFilePaths.ToList(),
                    projectId: 1,
                    filePath: "solution.sln")
            ]
        };
        return new ProjectContext(project);
    }

    private static CSharpProjectModel MakeTestProjectModel(string filePath) =>
        new CSharpProjectModel(
            projectReferences: [],
            documentFilePaths: [],
            buildTargets: ["net10.0"],
            buildMetadata: new ProjectBuildMetadataModel
            {
                IsTestProject = true,
                DefaultBuildTarget = "net10.0"
            },
            filePath: filePath);

    private static CSharpProjectModel MakeProductionProjectModel(string filePath) =>
        new CSharpProjectModel(
            projectReferences: [],
            documentFilePaths: [],
            buildTargets: ["net10.0"],
            buildMetadata: new ProjectBuildMetadataModel
            {
                IsTestProject = false,
                DefaultBuildTarget = "net10.0"
            },
            filePath: filePath);

    private static IStaticAnalysisWorkspace CreateWorkspace(
        string sourceCode,
        string documentFilePath,
        string projectFilePath,
        bool isTestProject)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var bcl = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId, VersionStamp.Create(), "Project", "Project", LanguageNames.CSharp,
            filePath: projectFilePath, metadataReferences: [bcl],
            parseOptions: parseOptions, compilationOptions: compilationOptions));
        solution = solution.AddDocument(
            DocumentId.CreateNewId(projectId), Path.GetFileName(documentFilePath),
            SourceText.From(sourceCode), filePath: documentFilePath);

        return new InMemoryWorkspace(solution);
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

    private sealed class InMemoryWorkspace : IStaticAnalysisWorkspace
    {
        private readonly Solution _solution;
        public InMemoryWorkspace(Solution solution) => _solution = solution;
        public IReadOnlyList<string> WorkspaceFailures => [];
        public void ClearWorkspaceFailures() { }
        public Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_solution);
        public Task<Project> OpenProjectAsync(string projectPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_solution.Projects.First(p =>
                string.Equals(p.FilePath, projectPath, StringComparison.OrdinalIgnoreCase)));
        public Task<Project> RefreshProjectAsync(string projectPath, CancellationToken cancellationToken = default)
            => OpenProjectAsync(projectPath, cancellationToken);
    }
}
