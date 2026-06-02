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

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_ForStaticTypeMethod_UsesStaticCallTargetAndExpectedExceptionNodes()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var sourceFilePath = Path.Combine(tempDirectory, "Predicates.cs");
            var projectFilePath = Path.Combine(tempDirectory, "Demo.csproj");
            await File.WriteAllTextAsync(sourceFilePath, string.Empty);
            await File.WriteAllTextAsync(projectFilePath, string.Empty);

            const string source = """
                                  using System;

                                  namespace Demo;

                                  public delegate bool AspectPredicate(object methodInfo);

                                  public static class Predicates
                                  {
                                      public static AspectPredicate Implement(Type baseOrInterfaceType)
                                      {
                                          if (baseOrInterfaceType == null)
                                          {
                                              throw new ArgumentNullException(nameof(baseOrInterfaceType));
                                          }

                                          if (baseOrInterfaceType.IsSealed)
                                          {
                                              throw new ArgumentException("The base type is not allowed to be Sealed.");
                                          }

                                          return methodInfo => true;
                                      }
                                  }
                                  """;

            var workspace = new InMemoryStaticAnalysisWorkspace(sourceFilePath, projectFilePath, source);
            var sourceStartLine = source[..source.IndexOf("public static AspectPredicate Implement", StringComparison.Ordinal)]
                .Count(x => x == '\n');
            var graph = await new ContextGraphService(workspace).BuildAsync(new TestGenerationRequest
            {
                MethodBody = string.Empty,
                MethodName = "Implement",
                MethodSignature = "public static AspectPredicate Implement(Type baseOrInterfaceType)",
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
                SourceEndLine = sourceStartLine + 20,
                Provider = AiProvider.OpenAi
            });

            Assert.Contains(graph.Nodes, x =>
                x.NodeId == "param:baseOrInterfaceType" &&
                x.TypeName == "Type" &&
                x.ConstructionHint.Contains("typeof"));
            var target = Assert.Single(graph.Nodes, x => x.NodeId == "sut");
            Assert.Equal("StaticCallTarget", target.NodeType);
            Assert.Null(target.VariableName);
            Assert.Empty(target.DependsOnNodeIds);
            Assert.Contains("no SUT instance", target.ConstructionHint);
            Assert.Contains(graph.Nodes, x => x.NodeType == "ExpectedException" && x.TypeName == "ArgumentNullException");
            Assert.Contains(graph.Nodes, x => x.NodeType == "ExpectedException" && x.TypeName == "ArgumentException");

            var results = new ContextResolutionService().Resolve(graph);
            Assert.Contains(results, x =>
                x.NodeId == "param:baseOrInterfaceType" &&
                x.CodeSnippet == "var baseOrInterfaceType = typeof(object);");
            Assert.Contains(results, x => x.NodeId == "sut" && x.CodeSnippet == string.Empty);
            Assert.DoesNotContain(results, x => x.CodeSnippet.Contains("new Predicates", StringComparison.Ordinal));
            Assert.DoesNotContain(results, x => x.CodeSnippet.Contains("new Type", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_WithAbstractClassParameter_ResolvesToConcreteSubtype()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var sourceFilePath = Path.Combine(tempDirectory, "Drawer.cs");
            var projectFilePath = Path.Combine(tempDirectory, "Demo.csproj");
            await File.WriteAllTextAsync(sourceFilePath, string.Empty);
            await File.WriteAllTextAsync(projectFilePath, string.Empty);

            // Circle has a parameterless constructor (score 0), Square requires a double (score 1).
            // FindConcreteSubtype should pick Circle as the simplest concrete stand-in.
            const string source = """
                                  namespace Demo;

                                  public abstract class Shape { }
                                  public sealed class Circle : Shape { }
                                  public sealed class Square : Shape { public Square(double side) {} }

                                  public sealed class Drawer
                                  {
                                      public void Draw(Shape shape) { }
                                  }
                                  """;

            var workspace = new InMemoryStaticAnalysisWorkspace(sourceFilePath, projectFilePath, source);
            var service = new ContextGraphService(workspace);
            var sourceStartLine = source[..source.IndexOf("public void Draw", StringComparison.Ordinal)]
                .Count(x => x == '\n');

            var graph = await service.BuildAsync(new TestGenerationRequest
            {
                MethodBody = string.Empty,
                MethodName = "Draw",
                MethodSignature = "public void Draw(Shape shape)",
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
                SourceEndLine = sourceStartLine + 3,
                Provider = AiProvider.OpenAi
            });

            var shapeNode = Assert.Single(graph.Nodes, x => x.NodeId == "param:shape");
            Assert.Equal("Circle", shapeNode.TypeName);
            Assert.False(shapeNode.RequiresMocking);
            Assert.True(shapeNode.IsResolved);
            Assert.Contains("Shape", shapeNode.ConstructionHint, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Circle", shapeNode.ConstructionHint, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_WithNoHelpersFoundInSupportContext_EmitsConstructFromScratchHint()
    {
        var service = new ContextGraphService();

        var graph = await service.BuildAsync(new TestGenerationRequest
        {
            MethodBody = "public int Add(int x, int y) => x + y;",
            MethodName = "Add",
            MethodSignature = "public int Add(int x, int y)",
            ContainingClass = "public class Calculator { }",
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = "No setup helpers found in the test project.",
            TestFramework = "xUnit",
            TestDependencies = string.Empty,
            CoverageGapSummary = string.Empty,
            Provider = AiProvider.OpenAi
        });

        var fixtureNode = Assert.Single(graph.Nodes, x => x.NodeType == "FixtureHint");
        Assert.Contains("scratch", fixtureNode.ConstructionHint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reuse", fixtureNode.ConstructionHint, StringComparison.OrdinalIgnoreCase);
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
