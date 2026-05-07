using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TestMap.Models.Experiment;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestGeneration.TargetSelection;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.UnitTests.TestGeneration;

public sealed class RoslynGeneratedTestValidationServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAfterApplicationAsync_DiffsDiagnosticsAndFailsOnNewError()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var testFilePath = Path.Combine(tempDirectory, "CalculatorTests.cs");
            var testProjectPath = Path.Combine(tempDirectory, "Demo.Tests.csproj");
            const string beforeSource = """
                                        namespace Demo.Tests;

                                        public sealed class CalculatorTests
                                        {
                                            public void Existing()
                                            {
                                            }
                                        }
                                        """;
            const string afterSource = """
                                       namespace Demo.Tests;

                                       public sealed class CalculatorTests
                                       {
                                           public void Existing()
                                           {
                                           }

                                           public void Generated()
                                           {
                                               MissingType value = null;
                                           }
                                       }
                                       """;

            await File.WriteAllTextAsync(testFilePath, beforeSource);
            await File.WriteAllTextAsync(testProjectPath, string.Empty);

            var workspace = new InMemoryStaticAnalysisWorkspace(testFilePath, testProjectPath, beforeSource);
            var service = new RoslynGeneratedTestValidationService(workspace);
            var context = CreateCandidateContext(testFilePath, testProjectPath);

            var before = await service.CaptureBeforeAsync(context);
            await File.WriteAllTextAsync(testFilePath, afterSource);

            var result = await service.ValidateAfterApplicationAsync(context, before);

            Assert.True(before.Captured);
            Assert.DoesNotContain(before.Diagnostics, x => x.Severity == DiagnosticSeverity.Error.ToString());
            Assert.False(result.Succeeded);
            Assert.False(result.Skipped);
            Assert.Contains(result.NewDiagnostics, x =>
                x.Id == "CS0246" &&
                x.Severity == DiagnosticSeverity.Error.ToString() &&
                x.Message.Contains("MissingType", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAfterApplicationAsync_AllowsExistingDiagnostics()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var testFilePath = Path.Combine(tempDirectory, "CalculatorTests.cs");
            var testProjectPath = Path.Combine(tempDirectory, "Demo.Tests.csproj");
            const string beforeSource = """
                                        namespace Demo.Tests;

                                        public sealed class CalculatorTests
                                        {
                                            public void Existing()
                                            {
                                                ExistingMissingType value = null;
                                            }
                                        }
                                        """;

            await File.WriteAllTextAsync(testFilePath, beforeSource);
            await File.WriteAllTextAsync(testProjectPath, string.Empty);

            var workspace = new InMemoryStaticAnalysisWorkspace(testFilePath, testProjectPath, beforeSource);
            var service = new RoslynGeneratedTestValidationService(workspace);
            var context = CreateCandidateContext(testFilePath, testProjectPath);

            var before = await service.CaptureBeforeAsync(context);
            var result = await service.ValidateAfterApplicationAsync(context, before);

            Assert.True(before.Captured);
            Assert.Contains(before.Diagnostics, x => x.Id == "CS0246");
            Assert.True(result.Succeeded);
            Assert.Empty(result.NewDiagnostics);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAfterApplicationAsync_DoesNotTreatShiftedExistingDiagnosticsAsNew()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var testFilePath = Path.Combine(tempDirectory, "CalculatorTests.cs");
            var testProjectPath = Path.Combine(tempDirectory, "Demo.Tests.csproj");
            const string beforeSource = """
                                        namespace Demo.Tests;

                                        public sealed class CalculatorTests
                                        {
                                            public void Existing()
                                            {
                                                ExistingMissingType value = null;
                                            }
                                        }
                                        """;
            const string afterSource = """
                                       namespace Demo.Tests;

                                       public sealed class CalculatorTests
                                       {
                                           public void Generated()
                                           {
                                               NewMissingType value = null;
                                           }

                                           public void Existing()
                                           {
                                               ExistingMissingType value = null;
                                           }
                                       }
                                       """;

            await File.WriteAllTextAsync(testFilePath, beforeSource);
            await File.WriteAllTextAsync(testProjectPath, string.Empty);

            var workspace = new InMemoryStaticAnalysisWorkspace(testFilePath, testProjectPath, beforeSource);
            var service = new RoslynGeneratedTestValidationService(workspace);
            var context = CreateCandidateContext(testFilePath, testProjectPath);

            var before = await service.CaptureBeforeAsync(context);
            await File.WriteAllTextAsync(testFilePath, afterSource);

            var result = await service.ValidateAfterApplicationAsync(context, before);

            Assert.Single(result.NewDiagnostics);
            Assert.Contains(result.NewDiagnostics, x =>
                x.Id == "CS0246" &&
                x.Message.Contains("NewMissingType", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ValidateAfterApplicationAsync_IgnoresAdditionalAssertResolutionErrorsWhenBaselineAlreadyHasThem()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var testFilePath = Path.Combine(tempDirectory, "CalculatorTests.cs");
            var testProjectPath = Path.Combine(tempDirectory, "Demo.Tests.csproj");
            const string beforeSource = """
                                        namespace Demo.Tests;

                                        public sealed class CalculatorTests
                                        {
                                            public void Existing()
                                            {
                                                Assert.True(true);
                                            }
                                        }
                                        """;
            const string afterSource = """
                                       namespace Demo.Tests;

                                       public sealed class CalculatorTests
                                       {
                                           public void Existing()
                                           {
                                               Assert.True(true);
                                           }

                                           public void Generated()
                                           {
                                               Assert.Equal(1, 1);
                                           }
                                       }
                                       """;

            await File.WriteAllTextAsync(testFilePath, beforeSource);
            await File.WriteAllTextAsync(testProjectPath, string.Empty);

            var workspace = new InMemoryStaticAnalysisWorkspace(testFilePath, testProjectPath, beforeSource);
            var service = new RoslynGeneratedTestValidationService(workspace);
            var context = CreateCandidateContext(testFilePath, testProjectPath);

            var before = await service.CaptureBeforeAsync(context);
            await File.WriteAllTextAsync(testFilePath, afterSource);

            var result = await service.ValidateAfterApplicationAsync(context, before);

            Assert.Contains(before.Diagnostics, x =>
                x.Id == "CS0103" &&
                x.Message.Contains("Assert", StringComparison.Ordinal));
            Assert.True(result.Succeeded);
            Assert.DoesNotContain(result.NewDiagnostics, x =>
                x.Id == "CS0103" &&
                x.Message.Contains("Assert", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CaptureBeforeAsync_SkipsWhenWorkspaceReportsProjectLoadFailure()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var testFilePath = Path.Combine(tempDirectory, "CalculatorTests.cs");
            var testProjectPath = Path.Combine(tempDirectory, "Demo.Tests.csproj");
            const string source = """
                                  using Xunit;

                                  public sealed class CalculatorTests
                                  {
                                      [Fact]
                                      public void Existing()
                                      {
                                      }
                                  }
                                  """;

            await File.WriteAllTextAsync(testFilePath, source);
            await File.WriteAllTextAsync(testProjectPath, string.Empty);

            var workspace = new InMemoryStaticAnalysisWorkspace(testFilePath, testProjectPath, source)
            {
                WorkspaceFailures = ["Failure: Package xunit could not be resolved."]
            };
            var service = new RoslynGeneratedTestValidationService(workspace);
            var context = CreateCandidateContext(testFilePath, testProjectPath);

            var before = await service.CaptureBeforeAsync(context);

            Assert.False(before.Captured);
            Assert.Contains("MSBuild workspace failure", before.SkipReason, StringComparison.Ordinal);
            Assert.Contains("xunit", before.SkipReason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static CandidateMethodContext CreateCandidateContext(string testFilePath, string testProjectPath)
    {
        return new CandidateMethodContext
        {
            Method = new CandidateMethod
            {
                MemberId = 7,
                MethodName = "Add",
                SourceCode = "public int Add(int x, int y) => x + y;",
                Signature = "public int Add(int x, int y)",
                BaselineCoverage = 0.42
            },
            MethodSignature = "public int Add(int x, int y)",
            ContainingClass = "public class Calculator { }",
            TestNamespace = "Demo.Tests",
            TestClassName = "CalculatorTests",
            TestFilePath = testFilePath,
            SourceFilePath = "Calculator.cs",
            SourceLocation = new CandidateSourceLocation
            {
                SourceFilePath = "Calculator.cs",
                StartLine = 1,
                EndLine = 1
            },
            SourceProjectPath = "Demo.csproj",
            TestProjectPath = testProjectPath,
            TargetBuildFramework = "net10.0",
            SolutionFilePath = string.Empty,
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = string.Empty,
            TestFramework = "xUnit",
            TestDependencies = string.Empty,
            CoverageGapSummary = string.Empty
        };
    }

    private sealed class InMemoryStaticAnalysisWorkspace : IStaticAnalysisWorkspace
    {
        private readonly Project _project;

        public InMemoryStaticAnalysisWorkspace(string testFilePath, string testProjectPath, string source)
        {
            var workspace = new AdhocWorkspace();
            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "Demo.Tests",
                "Demo.Tests",
                LanguageNames.CSharp,
                filePath: testProjectPath,
                metadataReferences:
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                ],
                parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var project = workspace.AddProject(projectInfo);
            var documentInfo = DocumentInfo.Create(
                DocumentId.CreateNewId(project.Id),
                "CalculatorTests.cs",
                filePath: testFilePath,
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

        public IReadOnlyList<string> WorkspaceFailures { get; set; } = [];

        public void ClearWorkspaceFailures()
        {
        }
    }
}
