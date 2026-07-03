using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models.Experiment;
using TestMap.Models.Generation;
using TestMap.Services.TestGeneration.Editing;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.TestGeneration;

public sealed class BasicExtensionPatchApplicationServiceTests
{
    // ---------------------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_ValidPatch_ReturnsTrueWithCountsAndUpdatesFile()
    {
        /// <summary>
        /// A patch with one new using, one helper, and one test method applied to a
        /// file with an existing class produces Success=true, correct counts, and
        /// a file that contains all three additions.
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            using System;

            namespace Demo.Tests;

            public sealed class CalculatorTests
            {
                [Fact]
                public void Existing_Test()
                {
                    Assert.True(true);
                }
            }
            """);
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(filePath),
            new BasicExtensionPatch
            {
                UsingsToAdd = ["System.IO"],
                HelperMethodsToAdd = ["private static string BuildHelper() => \"value\";"],
                TestMethod = "[Fact]\npublic void Add_ReturnsSum()\n{\n    Assert.Equal(3, 1 + 2);\n}",
                TestMethodName = "Add_ReturnsSum",
                IntegrationRationale = "Needs System.IO for stream helper."
            });

        Assert.True(result.Success);
        Assert.Equal("Success", result.PatchApplicationOutcome);
        Assert.Equal(1, result.AppliedUsingCount);
        Assert.Equal(1, result.AppliedHelperCount);
        Assert.Equal("Add_ReturnsSum", result.AppliedTestMethodName);
        Assert.NotNull(result.OriginalFileContents);
        Assert.Contains("using System;", result.OriginalFileContents);

        var root = ParseFile(filePath);
        var targetClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.Text == "CalculatorTests");
        var methodNames = targetClass.Members.OfType<MethodDeclarationSyntax>()
            .Select(m => m.Identifier.Text).ToList();

        Assert.Contains("Add_ReturnsSum", methodNames);
        Assert.Contains("BuildHelper", methodNames);
        Assert.Contains(root.Usings, u => u.Name!.ToString() == "System.IO");
    }

    // ---------------------------------------------------------------------------
    // Using deduplication
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_UsingAlreadyInFile_SkipsUsingAndReturnsZeroUsingCount()
    {
        /// <summary>
        /// When a using requested by the patch already appears in the file, the applier
        /// records a UsingSkipped decision and AppliedUsingCount stays zero.
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            using System;

            public sealed class CalculatorTests
            {
            }
            """);
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(filePath),
            new BasicExtensionPatch
            {
                UsingsToAdd = ["System"],
                TestMethod = "[Fact]\npublic void NewTest() { }"
            });

        Assert.True(result.Success);
        Assert.Equal(0, result.AppliedUsingCount);
        Assert.Contains(result.RuleDecisions, d => d.Value == "UsingSkipped");

        // Verify no duplicate using was written.
        var root = ParseFile(filePath);
        Assert.Single(root.Usings, u => u.Name!.ToString() == "System");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_NewUsing_AddsUsingAndReturnsUsingCountOne()
    {
        /// <summary>
        /// When a using in the patch is absent from the file, it is added and
        /// AppliedUsingCount equals one.
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            using System;

            public sealed class CalculatorTests
            {
            }
            """);
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(filePath),
            new BasicExtensionPatch
            {
                UsingsToAdd = ["System.IO"],
                TestMethod = "[Fact]\npublic void NewTest() { }"
            });

        Assert.True(result.Success);
        Assert.Equal(1, result.AppliedUsingCount);
        Assert.Contains(ParseFile(filePath).Usings, u => u.Name!.ToString() == "System.IO");
    }

    // ---------------------------------------------------------------------------
    // Failure cases — helper validation
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_MalformedHelper_ReturnsMalformedHelperOutcome()
    {
        /// <summary>
        /// A helper string that does not parse as a method declaration causes the applier
        /// to return Success=false with PatchApplicationOutcome="MalformedHelper"
        /// and to leave the file unchanged.
        /// </summary>
        using var dir = new TempTestDirectory();
        var originalContent = """
            public sealed class CalculatorTests
            {
            }
            """;
        var filePath = dir.WriteFile("CalculatorTests.cs", originalContent);
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(filePath),
            new BasicExtensionPatch
            {
                HelperMethodsToAdd = ["this is not a method { ]"],
                TestMethod = "[Fact]\npublic void NewTest() { }"
            });

        Assert.False(result.Success);
        Assert.Equal("MalformedHelper", result.PatchApplicationOutcome);
        Assert.Equal(originalContent, File.ReadAllText(filePath)); // file unchanged
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_DuplicateHelperName_ReturnsDuplicateHelperOutcome()
    {
        /// <summary>
        /// A helper whose name already exists in the target class causes the applier
        /// to return Success=false with PatchApplicationOutcome="DuplicateHelper".
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            public sealed class CalculatorTests
            {
                private void ExistingHelper() { }
            }
            """);
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(filePath),
            new BasicExtensionPatch
            {
                HelperMethodsToAdd = ["private void ExistingHelper() { /* duplicate */ }"],
                TestMethod = "[Fact]\npublic void NewTest() { }"
            });

        Assert.False(result.Success);
        Assert.Equal("DuplicateHelper", result.PatchApplicationOutcome);
    }

    // ---------------------------------------------------------------------------
    // Failure cases — test method validation
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_MalformedTestMethod_ReturnsMalformedTestMethodOutcome()
    {
        /// <summary>
        /// A test method string that does not parse as a method declaration causes the
        /// applier to return Success=false with PatchApplicationOutcome="MalformedTestMethod"
        /// and to leave the file unchanged.
        /// </summary>
        using var dir = new TempTestDirectory();
        var originalContent = """
            public sealed class CalculatorTests
            {
            }
            """;
        var filePath = dir.WriteFile("CalculatorTests.cs", originalContent);
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(filePath),
            new BasicExtensionPatch
            {
                TestMethod = "not a method at all {{ broken"
            });

        Assert.False(result.Success);
        Assert.Equal("MalformedTestMethod", result.PatchApplicationOutcome);
        Assert.Equal(originalContent, File.ReadAllText(filePath)); // file unchanged
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_DuplicateTestMethodName_ReturnsDuplicateTestMethodOutcome()
    {
        /// <summary>
        /// A test method whose name already exists in the target class causes the applier
        /// to return Success=false with PatchApplicationOutcome="DuplicateTestMethod".
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            public sealed class CalculatorTests
            {
                [Fact]
                public void ExistingTest() { }
            }
            """);
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(filePath),
            new BasicExtensionPatch
            {
                TestMethod = "[Fact]\npublic void ExistingTest() { /* duplicate */ }"
            });

        Assert.False(result.Success);
        Assert.Equal("DuplicateTestMethod", result.PatchApplicationOutcome);
    }

    // ---------------------------------------------------------------------------
    // Failure cases — structural
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_TargetClassNotFound_ReturnsTargetClassMissingOutcome()
    {
        /// <summary>
        /// When the class named in the context does not exist in the file, the applier
        /// returns Success=false with PatchApplicationOutcome="TargetClassMissing".
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            public sealed class WrongClassName
            {
            }
            """);
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(filePath, testClassName: "CalculatorTests"),
            new BasicExtensionPatch
            {
                TestMethod = "[Fact]\npublic void NewTest() { }"
            });

        Assert.False(result.Success);
        Assert.Equal("TargetClassMissing", result.PatchApplicationOutcome);
        Assert.NotNull(result.OriginalFileContents); // file was read before failing
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_FileNotFound_ReturnsTestFileMissingOutcome()
    {
        /// <summary>
        /// When the test file path does not exist on disk, the applier returns
        /// Success=false with PatchApplicationOutcome="TestFileMissing" and does not throw.
        /// </summary>
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(@"C:\NonExistent\Path\DoesNotExist.cs"),
            new BasicExtensionPatch
            {
                TestMethod = "[Fact]\npublic void NewTest() { }"
            });

        Assert.False(result.Success);
        Assert.Equal("TestFileMissing", result.PatchApplicationOutcome);
        Assert.Null(result.OriginalFileContents); // file was never read
    }

    // ---------------------------------------------------------------------------
    // Edge cases
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public void Apply_EmptyUsingsAndHelpers_StillAppliesTestMethodWithZeroCounts()
    {
        /// <summary>
        /// A patch with empty UsingsToAdd and HelperMethodsToAdd still applies the
        /// test method and returns Success=true with zero counts.
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            public sealed class CalculatorTests
            {
            }
            """);
        var service = new BasicExtensionPatchApplicationService();

        var result = service.Apply(
            CreateContext(filePath),
            new BasicExtensionPatch
            {
                UsingsToAdd = [],
                HelperMethodsToAdd = [],
                TestMethod = "[Fact]\npublic void Standalone_Test() { }"
            });

        Assert.True(result.Success);
        Assert.Equal(0, result.AppliedUsingCount);
        Assert.Equal(0, result.AppliedHelperCount);
        Assert.Equal("Standalone_Test", result.AppliedTestMethodName);

        var root = ParseFile(filePath);
        var targetClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.Text == "CalculatorTests");
        Assert.Contains(targetClass.Members.OfType<MethodDeclarationSyntax>(),
            m => m.Identifier.Text == "Standalone_Test");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static CandidateMethodContext CreateContext(
        string testFilePath,
        string testClassName = "CalculatorTests")
    {
        return new CandidateMethodContext
        {
            Method = new CandidateMethod
            {
                MemberId = 1,
                MethodName = "Add",
                SourceCode = "public int Add(int x, int y) => x + y;",
                Signature = "public int Add(int x, int y)",
                BaselineCoverage = 0.0
            },
            MethodSignature = "public int Add(int x, int y)",
            ContainingClass = "Calculator",
            TestNamespace = "Demo.Tests",
            TestClassName = testClassName,
            TestFilePath = testFilePath,
            SourceFilePath = "Calculator.cs",
            SourceLocation = new CandidateSourceLocation
            {
                SourceFilePath = "Calculator.cs",
                StartLine = 1,
                EndLine = 1
            },
            SourceProjectPath = "Demo.csproj",
            TestProjectPath = "Demo.Tests.csproj",
            TargetBuildFramework = "net10.0",
            SolutionFilePath = string.Empty,
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = string.Empty,
            TestFileContents = string.Empty,
            TestSupportContext = string.Empty,
            TestFramework = "xUnit",
            TestDependencies = "using Xunit;",
            CoverageGapSummary = string.Empty
        };
    }

    private static CompilationUnitSyntax ParseFile(string filePath) =>
        CSharpSyntaxTree.ParseText(File.ReadAllText(filePath)).GetCompilationUnitRoot();

    /// <summary>
    /// Scoped temp directory that is deleted on dispose.
    /// </summary>
    private sealed class TempTestDirectory : IDisposable
    {
        private readonly string _root =
            Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));

        public TempTestDirectory() => Directory.CreateDirectory(_root);

        public string WriteFile(string fileName, string content)
        {
            var path = Path.Combine(_root, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
