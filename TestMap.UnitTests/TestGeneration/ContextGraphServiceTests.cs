using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TestMap.Models.Configuration.AiProviders;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestGeneration;
using TestMap.Services.TestGeneration.Context;

namespace TestMap.UnitTests.TestGeneration;

public sealed class ContextGraphServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_ExtractsParametersSutFactoriesAndFixtureHints()
    {
        var service = new ContextGraphService();

        var graph = await service.BuildAsync(new TestGenerationRequest
        {
            MethodBody = "public int Add(int x, int y) => x + y;",
            MethodName = "Add",
            MethodSignature = "public int Add(int x, IClock clock)",
            ContainingClass = "public class Calculator { public Calculator(IClock clock) {} public static Calculator Create() => new Calculator(null); }",
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = "private readonly CalculatorBuilder builder = new();",
            TestFramework = "xUnit",
            TestDependencies = string.Empty,
            CoverageGapSummary = string.Empty,
            Provider = AiProvider.OpenAi
        });

        Assert.Contains(graph.Nodes, x => x.NodeId == "param:x" && x.TypeName == "int");
        Assert.Contains(graph.Nodes, x => x.NodeId == "param:clock" && x.RequiresMocking);
        Assert.Contains(graph.Nodes, x => x.NodeId == "sut" && x.TypeName == "Calculator");
        Assert.Contains(graph.Nodes, x => x.NodeId == "factory:Create");
        Assert.Contains(graph.Nodes, x => x.NodeType == "FixtureHint");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_WithRoslynWorkspace_LoadsExactDocumentAndBuildsSemanticGraph()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var sourceFilePath = Path.Combine(tempDirectory, "Calculator.cs");
            var projectFilePath = Path.Combine(tempDirectory, "Demo.csproj");
            await File.WriteAllTextAsync(sourceFilePath, string.Empty);
            await File.WriteAllTextAsync(projectFilePath, string.Empty);

            const string source = """
                                  namespace Demo;

                                  public interface IClock { }

                                  public sealed class Dependency { }

                                  public sealed class Calculator
                                  {
                                      public Calculator(IClock clock) { }

                                      public static Calculator Create(IClock clock) => new Calculator(clock);

                                      public int Other(IClock clock) => 0;

                                      public int Add(int x, IClock clock)
                                      {
                                          var dependency = new Dependency();
                                          return x;
                                      }
                                  }
                                  """;

            var workspace = new InMemoryStaticAnalysisWorkspace(sourceFilePath, projectFilePath, source);
            var service = new ContextGraphService(workspace);
            var sourceStartLine = source[..source.IndexOf("public int Add", StringComparison.Ordinal)]
                .Count(x => x == '\n');

            var graph = await service.BuildAsync(new TestGenerationRequest
            {
                MethodBody = string.Empty,
                MethodName = "Add",
                MethodSignature = "public int Add()",
                ContainingClass = string.Empty,
                ExampleTest = string.Empty,
                ExampleTestMetadataSummary = string.Empty,
                ProjectTestMetadataSummary = string.Empty,
                TestClass = string.Empty,
                TestFileContents = string.Empty,
                TestSupportContext = string.Empty,
                TestFramework = "xUnit",
                TestDependencies = string.Empty,
                CoverageGapSummary = string.Empty,
                SourceFilePath = sourceFilePath,
                SourceProjectPath = projectFilePath,
                SourceStartLine = sourceStartLine,
                SourceEndLine = sourceStartLine + 5,
                Provider = AiProvider.OpenAi
            });

            Assert.Contains(graph.Nodes, x => x.NodeId == "param:x" && x.TypeName == "int" && !x.RequiresMocking);
            Assert.Contains(graph.Nodes, x => x.NodeId == "param:clock" && x.TypeName == "IClock" && x.RequiresMocking);
            Assert.Contains(graph.Nodes, x => x.NodeId == "sut" && x.TypeName == "Calculator");
            Assert.Contains(graph.Nodes, x => x.NodeId == "factory:Create" && x.NodeType == "StaticFactory");
            Assert.Contains(graph.Nodes, x => x.NodeType == "ConstructedDependency" && x.TypeName == "Dependency");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Resolve_ProducesDeterministicSnippets()
    {
        var graph = await new ContextGraphService().BuildAsync(new TestGenerationRequest
        {
            MethodBody = "public int Add(int x, string name) => x;",
            MethodName = "Add",
            MethodSignature = "public int Add(int x, string name)",
            ContainingClass = "public class Calculator { }",
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = string.Empty,
            TestFramework = "xUnit",
            TestDependencies = string.Empty,
            CoverageGapSummary = string.Empty,
            Provider = AiProvider.OpenAi
        });

        var results = new ContextResolutionService().Resolve(graph);

        Assert.Contains(results, x => x.NodeId == "param:x" && x.CodeSnippet == "var x = 1;");
        Assert.Contains(results, x => x.NodeId == "param:name" && x.CodeSnippet == "var name = \"value\";");
        Assert.Contains(results, x => x.NodeId == "sut" && x.CodeSnippet == "var sut = new Calculator();");
    }

    private sealed class InMemoryStaticAnalysisWorkspace : IStaticAnalysisWorkspace
    {
        private readonly Project _project;

        public InMemoryStaticAnalysisWorkspace(string sourceFilePath, string projectFilePath, string source)
        {
            var workspace = new AdhocWorkspace();
            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "Demo",
                "Demo",
                LanguageNames.CSharp,
                filePath: projectFilePath,
                metadataReferences:
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
                ],
                parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var project = workspace.AddProject(projectInfo);
            var documentInfo = DocumentInfo.Create(
                DocumentId.CreateNewId(project.Id),
                "Calculator.cs",
                filePath: sourceFilePath,
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())));
            _project = workspace.AddDocument(documentInfo).Project;
        }

        public Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_project.Solution);
        }

        public Task<Project> OpenProjectAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_project);
        }

        public Task<Project> RefreshProjectAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_project);
        }

        public IReadOnlyList<string> WorkspaceFailures { get; } = [];

        public void ClearWorkspaceFailures()
        {
        }
    }
}
