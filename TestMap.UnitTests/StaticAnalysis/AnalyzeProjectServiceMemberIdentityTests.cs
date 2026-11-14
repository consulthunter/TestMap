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
/// Member identity in <see cref="AnalyzeProjectService"/>. Pilot V8 found that same-name
/// overloads collapsed into one member row (gridify IsValid&lt;T&gt;, serilog TryCompile) and
/// that members declared in the second and later files of a partial type were never persisted
/// (zstring's ZString kept only ZString.Concat.cs). Both made source/test mappings point at the
/// wrong overload.
/// </summary>
public sealed class AnalyzeProjectServiceMemberIdentityTests
{
    private const string SourceProjPath = "src/Source/Source.csproj";
    private const string TestProjPath = "tests/Source.Tests/Source.Tests.csproj";

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_Overloads_PersistOneRowPerOverload()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        var solution = CreateSolution(
            [("src/Source/Expression.cs", """
                                          namespace Source;
                                          public static class Expression
                                          {
                                              public static bool TryCompile(string text, out int result) { result = 0; return true; }

                                              public static bool TryCompile(string text, string format, out int result) { result = 1; return true; }

                                              public static bool TryCompile(ref string text) { return false; }

                                              public static bool TryCompile<T>(T value) { return false; }
                                          }
                                          """)],
            []);
        var service = CreateService(db, solution);

        await service.AnalyzeProjectAsync(MakeProjectModel(SourceProjPath, []), new Dictionary<string, int>());

        var overloads = await db.Members
            .Where(x => x.Name == "TryCompile")
            .OrderBy(x => x.Location.StartLineNumber)
            .ToListAsync();

        Assert.Equal(4, overloads.Count);
        Assert.Equal(4, overloads.Select(x => x.Signature).Distinct().Count());
        Assert.Equal(4, overloads.Select(x => x.Location.StartLineNumber).Distinct().Count());
        Assert.Contains("string format", overloads[1].FullString);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_PartialTypeAcrossFiles_PersistsMembersOfEveryFile()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        var solution = CreateSolution(
            [
                ("src/Source/Text.Concat.cs", """
                                              namespace Source;
                                              public static partial class Text
                                              {
                                                  public static string Concat(string a, string b) => a + b;
                                              }
                                              """),
                ("src/Source/Text.cs", """
                                       namespace Source;
                                       public static partial class Text
                                       {
                                           public static string Join(string separator, string[] values) => string.Join(separator, values);

                                           public static string Format(string format, object arg) => string.Format(format, arg);
                                       }
                                       """)
            ],
            []);
        var service = CreateService(db, solution);

        await service.AnalyzeProjectAsync(MakeProjectModel(SourceProjPath, []), new Dictionary<string, int>());

        var members = await (
                from member in db.Members
                join sourceObject in db.Objects on member.ObjectEntityId equals sourceObject.Id
                join sourceFile in db.Files on sourceObject.FileId equals sourceFile.Id
                where sourceObject.Name == "Text"
                select new { member.Name, sourceFile.FilePath })
            .ToListAsync();

        Assert.Equal(3, members.Count);
        Assert.EndsWith("Text.Concat.cs", members.Single(x => x.Name == "Concat").FilePath);
        Assert.EndsWith("Text.cs", members.Single(x => x.Name == "Join").FilePath);
        Assert.EndsWith("Text.cs", members.Single(x => x.Name == "Format").FilePath);
        Assert.Equal(2, await db.Objects.CountAsync(x => x.Name == "Text"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeProjectAsync_Reanalysis_ReusesOverloadAndPartialRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        var solution = CreateSolution(
            [
                ("src/Source/Text.A.cs", """
                                         namespace Source;
                                         public partial class Text
                                         {
                                             public int Size(string value) => value.Length;
                                             public int Size(string value, int extra) => value.Length + extra;
                                         }
                                         """),
                ("src/Source/Text.B.cs", """
                                         namespace Source;
                                         public partial class Text
                                         {
                                             public int Count() => 0;
                                         }
                                         """)
            ],
            []);
        var service = CreateService(db, solution);
        var project = MakeProjectModel(SourceProjPath, []);

        await service.AnalyzeProjectAsync(project, new Dictionary<string, int>());
        var firstPass = await db.Members.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();

        await service.AnalyzeProjectAsync(project, new Dictionary<string, int>());
        var secondPass = await db.Members.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();

        Assert.Equal(3, firstPass.Count);
        Assert.Equal(firstPass, secondPass);
    }

    /// <summary>
    /// A test that calls one overload is mapped to that overload, and a test that calls a member
    /// declared in a later partial file is mapped to that member, not to a same-named sibling.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RefreshSourceTestMappingsAsync_OverloadAndPartialCalls_MapToTheCalledMember()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = await CreateDbAsync(connection);

        var solution = CreateSolution(
            [
                ("src/Source/Text.Concat.cs", """
                                              namespace Source;
                                              public static partial class Text
                                              {
                                                  public static string Concat<T1>(T1 arg1) => arg1?.ToString() ?? "";
                                              }
                                              """),
                ("src/Source/Text.cs", """
                                       namespace Source;
                                       public static partial class Text
                                       {
                                           public static bool TryCompile(string text, out string error) { error = ""; return true; }

                                           public static bool TryCompile(string text, string format, out string error) { error = format; return false; }

                                           public static string Concat<T>(System.Collections.Generic.List<T> values) => values.Count.ToString();
                                       }
                                       """)
            ],
            [
                ("tests/Source.Tests/TextTests.cs", """
                                                    namespace Source.Tests;
                                                    public class TextTests
                                                    {
                                                        [Fact]
                                                        public void TryCompile_Valid() { Source.Text.TryCompile("x", out _); }

                                                        [Fact]
                                                        public void Concat_List() { Source.Text.Concat(new System.Collections.Generic.List<string>()); }
                                                    }
                                                    """)
            ]);
        var service = CreateService(db, solution);
        var sharedMemberIds = new Dictionary<string, int>(StringComparer.Ordinal);

        await service.AnalyzeProjectAsync(MakeProjectModel(SourceProjPath, []), sharedMemberIds);
        await service.AnalyzeProjectAsync(MakeProjectModel(TestProjPath, [SourceProjPath]), sharedMemberIds);
        await service.RefreshSourceTestMappingsAsync(sharedMemberIds.Values, MemberAnalysisOrigin.Baseline());

        var mappings = await (
                from mapping in db.SourceTestMappings
                join source in db.Members on mapping.SourceMemberId equals source.Id
                join test in db.Members on mapping.TestMemberId equals test.Id
                select new { Test = test.Name, source.FullString })
            .ToListAsync();

        var tryCompile = Assert.Single(mappings, x => x.Test == "TryCompile_Valid");
        Assert.DoesNotContain("string format", tryCompile.FullString);

        var concat = Assert.Single(mappings, x => x.Test == "Concat_List");
        Assert.Contains("List<T> values", concat.FullString);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static async Task<TestMapDbContext> CreateDbAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestMapDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Projects.Add(new ProjectEntity
        {
            Id = 1, Owner = "owner", RepoName = "repo",
            DirectoryPath = ".", ContentHash = "project"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static Solution CreateSolution(
        IReadOnlyList<(string Path, string Code)> productionDocuments,
        IReadOnlyList<(string Path, string Code)> testDocuments)
    {
        var workspace = new AdhocWorkspace();
        var sourceProjId = ProjectId.CreateNewId();
        var testProjId = ProjectId.CreateNewId();
        var bcl = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            sourceProjId, VersionStamp.Create(), "Source", "Source", LanguageNames.CSharp,
            filePath: SourceProjPath, metadataReferences: [bcl],
            parseOptions: parseOptions, compilationOptions: compilationOptions));
        foreach (var (path, code) in productionDocuments)
            solution = solution.AddDocument(
                DocumentId.CreateNewId(sourceProjId), Path.GetFileName(path), SourceText.From(code), filePath: path);

        solution = solution.AddProject(ProjectInfo.Create(
            testProjId, VersionStamp.Create(), "Source.Tests", "Source.Tests", LanguageNames.CSharp,
            filePath: TestProjPath, metadataReferences: [bcl],
            projectReferences: [new ProjectReference(sourceProjId)],
            parseOptions: parseOptions, compilationOptions: compilationOptions));
        foreach (var (path, code) in testDocuments)
            solution = solution.AddDocument(
                DocumentId.CreateNewId(testProjId), Path.GetFileName(path), SourceText.From(code), filePath: path);

        return solution;
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

    private static AnalyzeProjectService CreateService(TestMapDbContext db, Solution solution)
    {
        var config = new TestMapConfig();
        config.RuntimeConfig.Frameworks = new Dictionary<string, List<string>>
        {
            ["xunit"] = ["Fact", "Theory", "InlineData"]
        };
        var project = new ProjectModel(config: config)
        {
            DbId = 1,
            Solutions =
            [
                new CSharpSolutionModel(
                    projects: [SourceProjPath, TestProjPath],
                    projectId: 1,
                    filePath: "solution.sln")
            ]
        };
        var context = new ProjectContext(project);
        var workspace = new InMemoryStaticAnalysisWorkspace(solution);
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

    private sealed class InMemoryStaticAnalysisWorkspace : IStaticAnalysisWorkspace
    {
        private readonly Solution _solution;

        public InMemoryStaticAnalysisWorkspace(Solution solution) => _solution = solution;

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
