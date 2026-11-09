using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Models.Experiment.Assertions;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.StaticAnalysis.Assertions;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.StaticAnalysis.Assertions;

internal sealed class AssertionLineageTestWorkspace
{
    internal const string SourceProjectPath = "src/Product/Product.csproj";
    internal const string TestProjectPath = "tests/Product.Tests/Product.Tests.csproj";
    internal const string SourceFilePath = "src/Product/Service.cs";
    internal const string TestFilePath = "tests/Product.Tests/ServiceTests.cs";

    private AssertionLineageTestWorkspace(
        Solution solution,
        Document sourceDocument,
        Document testDocument)
    {
        Solution = solution;
        SourceDocument = sourceDocument;
        TestDocument = testDocument;
    }

    public Solution Solution { get; }
    public Document SourceDocument { get; }
    public Document TestDocument { get; }

    public static AssertionLineageTestWorkspace Create(
        string testBody,
        string testSupport = "",
        string productionMembers = "public int GetValue() => 42;")
    {
        var source = $$"""
                       namespace Product;
                       public class Service
                       {
                           {{productionMembers}}
                       }
                       """;
        var test = $$"""
                     using System;
                     using System.Threading.Tasks;
                     using Xunit;
                     using FluentAssertions;
                     using Shouldly;
                     using NUnit.Framework;
                     using Microsoft.VisualStudio.TestTools.UnitTesting;

                     namespace Product.Tests
                     {
                         public class ServiceTests
                         {
                             public void Test()
                             {
                                 {{testBody}}
                             }

                             {{testSupport}}
                         }
                     }

                     namespace Xunit
                     {
                         public static class Assert
                         {
                             public static void Equal<T>(T expected, T actual, string? userMessage = null) { }
                             public static void True(bool condition, string? userMessage = null) { }
                             public static T Throws<T>(Action action) where T : Exception => default!;
                         }
                     }

                     namespace FluentAssertions
                     {
                         public static class AssertionExtensions
                         {
                             public static ObjectAssertions<T> Should<T>(this T actual) => new();
                         }
                         public sealed class ObjectAssertions<T>
                         {
                             public void Be(T expected, string because = "") { }
                         }
                     }

                     namespace Shouldly
                     {
                         public static class ShouldlyExtensions
                         {
                             public static void ShouldBe<T>(this T actual, T expected, string? customMessage = null) { }
                         }
                     }

                     namespace NUnit.Framework
                     {
                         public static class Assert
                         {
                             public static void That<T>(T actual, object constraint, string? message = null) { }
                         }
                         public static class Is
                         {
                             public static object EqualTo<T>(T expected) => new();
                         }
                     }

                     namespace Microsoft.VisualStudio.TestTools.UnitTesting
                     {
                         public static class Assert
                         {
                             public static void AreEqual<T>(T expected, T actual, string? message = null) { }
                         }
                     }
                     """;

        var workspace = new AdhocWorkspace();
        var sourceProjectId = ProjectId.CreateNewId();
        var testProjectId = ProjectId.CreateNewId();
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var references = CreateReferences();
        var solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(
                sourceProjectId,
                VersionStamp.Create(),
                "Product",
                "Product",
                LanguageNames.CSharp,
                filePath: SourceProjectPath,
                metadataReferences: references,
                parseOptions: parseOptions,
                compilationOptions: compilationOptions));
        var sourceDocumentId = DocumentId.CreateNewId(sourceProjectId);
        solution = solution.AddDocument(
                sourceDocumentId,
                "Service.cs",
                SourceText.From(source),
                filePath: SourceFilePath);

        solution = solution.AddProject(ProjectInfo.Create(
            testProjectId,
            VersionStamp.Create(),
            "Product.Tests",
            "Product.Tests",
            LanguageNames.CSharp,
            filePath: TestProjectPath,
            metadataReferences: references,
            projectReferences: [new ProjectReference(sourceProjectId)],
            parseOptions: parseOptions,
            compilationOptions: compilationOptions));
        var testDocumentId = DocumentId.CreateNewId(testProjectId);
        solution = solution.AddDocument(
            testDocumentId,
            "ServiceTests.cs",
            SourceText.From(test),
            filePath: TestFilePath);

        return new AssertionLineageTestWorkspace(
            solution,
            solution.GetDocument(sourceDocumentId)!,
            solution.GetDocument(testDocumentId)!);
    }

    public async Task<(SemanticModel Model, MethodDeclarationSyntax Method)> GetTestMethodAsync()
    {
        var root = await TestDocument.GetSyntaxRootAsync();
        var model = await TestDocument.GetSemanticModelAsync();
        var method = root!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(candidate => candidate.Identifier.Text == "Test");
        return (model!, method);
    }

    public async Task<InvocationExpressionSyntax> GetInvocationAsync(string methodName)
    {
        var (_, method) = await GetTestMethodAsync();
        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .First(invocation =>
                CSharpAnalysisRules.ExtractInvocationMethodName(invocation) == methodName);
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var paths = new[]
        {
            typeof(object).Assembly.Location,
            typeof(Action).Assembly.Location,
            typeof(Task).Assembly.Location,
            typeof(Enumerable).Assembly.Location
        };
        return paths
            .Distinct()
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();
    }

    internal sealed class WorkspaceAdapter(Solution solution) : IStaticAnalysisWorkspace
    {
        public IReadOnlyList<string> WorkspaceFailures => [];
        public void ClearWorkspaceFailures() { }
        public Task<Solution> OpenSolutionAsync(
            string solutionPath,
            CancellationToken cancellationToken = default) => Task.FromResult(solution);
        public Task<Project> OpenProjectAsync(
            string projectPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(solution.Projects.First(project => project.FilePath == projectPath));
        public Task<Project> RefreshProjectAsync(
            string projectPath,
            CancellationToken cancellationToken = default) =>
            OpenProjectAsync(projectPath, cancellationToken);
    }
}

internal sealed class AssertionLineageAnalyzerHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private AssertionLineageAnalyzerHarness(
        SqliteConnection connection,
        TestMapDbContext dbContext,
        AssertionLineageTestWorkspace workspace,
        IAssertionLineageAnalysisService analyzer,
        int testMemberId,
        int intendedSourceMemberId)
    {
        _connection = connection;
        DbContext = dbContext;
        Workspace = workspace;
        Analyzer = analyzer;
        TestMemberId = testMemberId;
        IntendedSourceMemberId = intendedSourceMemberId;
    }

    public TestMapDbContext DbContext { get; }
    public AssertionLineageTestWorkspace Workspace { get; }
    public IAssertionLineageAnalysisService Analyzer { get; }
    public int TestMemberId { get; }
    public int IntendedSourceMemberId { get; }

    public static async Task<AssertionLineageAnalyzerHarness> CreateAsync(
        string testBody,
        string testSupport = "",
        string productionMembers = "public int GetValue() => 42;",
        string intendedSourceMemberName = "GetValue")
    {
        var workspace = AssertionLineageTestWorkspace.Create(
            testBody,
            testSupport,
            productionMembers);
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new TestMapDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var memberIds = await SeedAsync(dbContext, workspace);
        var classifier = new AssertionLineageClassifier();
        var resolver = new RoslynProductionMemberResolver();
        var analyzer = new RoslynAssertionLineageAnalysisService(
            dbContext,
            new AssertionLineageTestWorkspace.WorkspaceAdapter(workspace.Solution),
            AssertionPatternCatalog.Shared,
            new AssertionOperandExtractor(),
            new AssertionLineageSlicer(resolver, classifier),
            classifier);
        return new AssertionLineageAnalyzerHarness(
            connection,
            dbContext,
            workspace,
            analyzer,
            memberIds["Test"],
            memberIds[intendedSourceMemberName]);
    }

    public Task<AssertionLineageAnalysisResult> AnalyzeAsync(
        IReadOnlyList<int>? testMemberIds = null,
        AssertionLineagePolicy? policy = null)
    {
        return Analyzer.AnalyzeAsync(new AssertionLineageAnalysisRequest
        {
            ProjectId = 1,
            SolutionId = 1,
            IntendedSourceMemberId = IntendedSourceMemberId,
            TestMemberIds = testMemberIds ?? [TestMemberId],
            Policy = policy ?? new AssertionLineagePolicy()
        });
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static async Task<Dictionary<string, int>> SeedAsync(
        TestMapDbContext dbContext,
        AssertionLineageTestWorkspace workspace)
    {
        dbContext.Projects.Add(new ProjectEntity
        {
            Id = 1,
            Owner = "owner",
            RepoName = "repo",
            DirectoryPath = ".",
            ContentHash = "project"
        });
        dbContext.CSharpSolutions.Add(new CSharpSolutionEntity
        {
            Id = 1,
            ProjectId = 1,
            FilePath = "repo.sln",
            ContentHash = "solution"
        });
        dbContext.CSharpProjects.AddRange(
            new CSharpProjectEntity
            {
                Id = 1,
                SolutionId = 1,
                FilePath = AssertionLineageTestWorkspace.SourceProjectPath,
                BuildMetadata = new ProjectBuildMetadataModel { DefaultBuildTarget = "net10.0" },
                ContentHash = "source-project"
            },
            new CSharpProjectEntity
            {
                Id = 2,
                SolutionId = 1,
                FilePath = AssertionLineageTestWorkspace.TestProjectPath,
                BuildMetadata = new ProjectBuildMetadataModel
                {
                    IsTestProject = true,
                    DefaultBuildTarget = "net10.0"
                },
                ContentHash = "test-project"
            });
        dbContext.Files.AddRange(
            new FileEntity
            {
                Id = 1,
                CSharpProjectId = 1,
                FilePath = AssertionLineageTestWorkspace.SourceFilePath,
                ContentHash = "source-file"
            },
            new FileEntity
            {
                Id = 2,
                CSharpProjectId = 2,
                FilePath = AssertionLineageTestWorkspace.TestFilePath,
                ContentHash = "test-file"
            });
        dbContext.Objects.AddRange(
            new ObjectEntity
            {
                Id = 1,
                FileId = 1,
                Namespace = "Product",
                Name = "Service",
                Kind = "class",
                FullString = "public class Service",
                ContentHash = "source-object"
            },
            new ObjectEntity
            {
                Id = 2,
                FileId = 2,
                Namespace = "Product.Tests",
                Name = "ServiceTests",
                Kind = "class",
                IsTestObject = true,
                TestFramework = "xUnit",
                FullString = "public class ServiceTests",
                ContentHash = "test-object"
            });

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var sourceRoot = await workspace.SourceDocument.GetSyntaxRootAsync();
        var nextSourceId = 10;
        foreach (var declaration in sourceRoot!.DescendantNodes()
                     .OfType<MethodDeclarationSyntax>())
        {
            var id = nextSourceId++;
            result[declaration.Identifier.Text] = id;
            dbContext.Members.Add(CreateMember(
                id,
                1,
                declaration.Identifier.Text,
                declaration,
                isTestMember: false,
                contentHash: $"source-{id}"));
        }

        var testRoot = await workspace.TestDocument.GetSyntaxRootAsync();
        var nextTestId = 20;
        var testDeclarations = testRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(declaration =>
                declaration.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()
                    ?.Identifier.Text == "ServiceTests");
        foreach (var declaration in testDeclarations)
        {
            var id = nextTestId++;
            result[declaration.Identifier.Text] = id;
            dbContext.Members.Add(CreateMember(
                id,
                2,
                declaration.Identifier.Text,
                declaration,
                isTestMember: declaration.Identifier.Text == "Test",
                contentHash: $"test-{id}"));
        }

        await dbContext.SaveChangesAsync();
        return result;
    }

    private static MemberEntity CreateMember(
        int id,
        int objectId,
        string name,
        MethodDeclarationSyntax declaration,
        bool isTestMember,
        string contentHash)
    {
        var span = declaration.GetLocation().GetLineSpan();
        return new MemberEntity
        {
            Id = id,
            ObjectEntityId = objectId,
            Name = name,
            Kind = "method",
            Modifiers = declaration.Modifiers.Select(modifier => modifier.Text).ToList(),
            FullString = declaration.ToFullString(),
            IsTestMember = isTestMember,
            IsGenerated = isTestMember,
            Location = new Location(
                span.StartLinePosition.Line,
                span.StartLinePosition.Character,
                span.EndLinePosition.Line,
                span.EndLinePosition.Character),
            ContentHash = contentHash
        };
    }
}
