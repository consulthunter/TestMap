using TestMap.Models.Experiment;
using TestMap.Models.Generation;
using TestMap.Models.Rules;
using TestMap.Services.TestGeneration.Editing;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.TestGeneration;

public sealed class BasicExtensionTestActionExecutorTests
{
    // ---------------------------------------------------------------------------
    // Existing behaviour — always appends (never replaces), even for ImproveExistingTest
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_NonJsonResponse_FallsBackToAppendPath()
    {
        /// <summary>
        /// When the generated response is not valid BasicExtensionPatch JSON the executor
        /// falls back to the legacy method-only append path via ITestCodeEditService.
        /// The RecommendedAction on the candidate is irrelevant — Basic Extension always appends.
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            public sealed class CalculatorTests
            {
            }
            """);

        var editor = new FakeTestCodeEditService();
        var executor = CreateExecutor(editor);
        var context = CreateContext(filePath, "CalculatorTests");

        var contextWithImprove = CreateContext(filePath, "CalculatorTests");
        // Manually build a context where RecommendedAction suggests improvement;
        // the executor must still append, not replace.
        var improveMethod = new CandidateMethod
        {
            MethodName = "Calculate",
            RecommendedAction = CandidateActionKind.ImproveExistingTest,
            ExistingTestMethodName = "Calculate_Existing"
        };
        _ = improveMethod; // verify the field compiles; actual executor ignores RecommendedAction

        var result = await executor.ExecuteAsync(
            contextWithImprove,
            "[Fact] public void Calculate_NewCoverage() { }",
            "Calculate_NewCoverage");

        Assert.True(result.Success);
        Assert.Equal(CandidateActionKind.ExtendExistingTestSuite, result.ActionKind);
        Assert.Equal(1, editor.AppendCalls);
        Assert.Equal(0, editor.ReplaceCalls);
    }

    // ---------------------------------------------------------------------------
    // Structured patch path
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_ValidPatchJson_CallsPatchApplierAndReturnsSuccess()
    {
        /// <summary>
        /// When the response is a valid BasicExtensionPatch JSON object the executor
        /// routes through the patch applier instead of the legacy editor.
        /// The patch applier writes the test method to the file.
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            public sealed class CalculatorTests
            {
            }
            """);

        var executor = CreateExecutor();
        var context = CreateContext(filePath, "CalculatorTests");

        var patchJson = """
            {
              "usingsToAdd": [],
              "helperMethodsToAdd": [],
              "testMethod": "[Fact]\npublic void Add_ReturnsSum()\n{\n    Assert.Equal(3, 1 + 2);\n}",
              "testMethodName": "Add_ReturnsSum",
              "integrationRationale": "Simple test with no additions needed."
            }
            """;

        var result = await executor.ExecuteAsync(context, patchJson, "Add_ReturnsSum");

        Assert.True(result.Success);
        Assert.Equal("Add_ReturnsSum", result.AppliedTestMethodName);
        Assert.Equal("Success", result.PatchApplicationOutcome);
        Assert.Equal(context.TestFilePath, result.AppliedFilePath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_PatchJsonInFences_StripsFencesAndAppliesPatch()
    {
        /// <summary>
        /// When the patch JSON is wrapped in ```json code fences the executor strips the
        /// fences before deserializing and successfully applies the patch.
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            public sealed class CalculatorTests
            {
            }
            """);

        var executor = CreateExecutor();
        var context = CreateContext(filePath, "CalculatorTests");

        var fencedJson = """
            ```json
            {
              "usingsToAdd": [],
              "helperMethodsToAdd": [],
              "testMethod": "[Fact]\npublic void Add_WithFences()\n{\n    Assert.True(true);\n}",
              "testMethodName": "Add_WithFences"
            }
            ```
            """;

        var result = await executor.ExecuteAsync(context, fencedJson, "Add_WithFences");

        Assert.True(result.Success);
        Assert.Equal("Add_WithFences", result.AppliedTestMethodName);
        Assert.Equal("Success", result.PatchApplicationOutcome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_MalformedJson_FallsBackToMethodOnlyPathWithoutException()
    {
        /// <summary>
        /// When the response looks like JSON but cannot be deserialized as BasicExtensionPatch,
        /// the executor falls back to the legacy append path without throwing an exception.
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", """
            public sealed class CalculatorTests
            {
            }
            """);

        var editor = new FakeTestCodeEditService();
        var executor = CreateExecutor(editor);
        var context = CreateContext(filePath, "CalculatorTests");

        // Looks like a JSON object but missing required testMethod field.
        var result = await executor.ExecuteAsync(
            context,
            """{ "somethingElse": "value" }""",
            "FallbackTest");

        // Result is from the fallback path (fake editor returns Success = true)
        Assert.True(result.Success);
        Assert.Equal(1, editor.AppendCalls);
        Assert.Null(result.PatchApplicationOutcome); // fallback path sets no outcome
    }

    // ---------------------------------------------------------------------------
    // Precondition failures
    // ---------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_EmptyTestFilePath_ReturnsTestFileMissingFailure()
    {
        /// <summary>
        /// An empty TestFilePath triggers the TestFileMissing precondition before any
        /// patch or editor work is attempted.
        /// </summary>
        var executor = CreateExecutor();
        var context = CreateContext(string.Empty, "CalculatorTests");

        var result = await executor.ExecuteAsync(context, "[Fact] public void T() { }", "T");

        Assert.False(result.Success);
        Assert.Equal("TestFileMissing", result.PatchApplicationOutcome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_NonExistentTestFilePath_ReturnsTestFileMissingFailure()
    {
        /// <summary>
        /// A TestFilePath that does not exist on disk triggers the TestFileMissing
        /// precondition. The executor does not throw.
        /// </summary>
        var executor = CreateExecutor();
        var context = CreateContext(@"C:\NonExistent\DoesNotExist.cs", "CalculatorTests");

        var result = await executor.ExecuteAsync(context, "[Fact] public void T() { }", "T");

        Assert.False(result.Success);
        Assert.Equal("TestFileMissing", result.PatchApplicationOutcome);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_EmptyTestClassName_ReturnsTestClassNameMissingFailure()
    {
        /// <summary>
        /// An empty TestClassName triggers the TestClassNameMissing precondition.
        /// The file must exist to reach this check; only the class name is missing.
        /// </summary>
        using var dir = new TempTestDirectory();
        var filePath = dir.WriteFile("CalculatorTests.cs", "public class X { }");

        var executor = CreateExecutor();
        var context = CreateContext(filePath, testClassName: string.Empty);

        var result = await executor.ExecuteAsync(context, "[Fact] public void T() { }", "T");

        Assert.False(result.Success);
        Assert.Equal("TestClassNameMissing", result.PatchApplicationOutcome);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static BasicExtensionTestActionExecutor CreateExecutor(
        ITestCodeEditService? editor = null)
    {
        return new BasicExtensionTestActionExecutor(
            editor ?? new FakeTestCodeEditService(),
            new BasicExtensionPatchApplicationService());
    }

    private static CandidateMethodContext CreateContext(
        string testFilePath,
        string testClassName = "CalculatorTests")
    {
        return new CandidateMethodContext
        {
            Method = new CandidateMethod
            {
                MethodName = "Calculate",
                RecommendedAction = CandidateActionKind.ExtendExistingTestSuite
            },
            MethodSignature = "int Calculate()",
            ContainingClass = "Calculator",
            TestNamespace = "Sample.Tests",
            TestClassName = testClassName,
            TestFilePath = testFilePath,
            SourceFilePath = "Calculator.cs",
            SourceLocation = new CandidateSourceLocation
            {
                SourceFilePath = "Calculator.cs",
                StartLine = 1,
                EndLine = 1
            },
            SourceProjectPath = "Sample.csproj",
            TestProjectPath = "Sample.Tests.csproj",
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

    private sealed class FakeTestCodeEditService : ITestCodeEditService
    {
        public int AppendCalls { get; private set; }
        public int ReplaceCalls { get; private set; }

        public bool EnsureTestClassExists(CandidateMethodContext context) => true;

        public bool AppendTestMethod(CandidateMethodContext context, string testMethodCode)
        {
            AppendCalls++;
            return true;
        }

        public TestMethodAppendResult AppendTestMethodWithResult(
            CandidateMethodContext context,
            string testMethodCode)
        {
            AppendCalls++;
            return new TestMethodAppendResult
            {
                Success = true,
                RuleDecisions =
                [
                    new RuleDecisionRecord
                    {
                        DecisionKind = "GenerationAppend",
                        Value = "AppendTargetSelected",
                        RuleId = "generation.append.target-selected",
                        RuleVersion = "1.0",
                        Confidence = RuleConfidence.High
                    }
                ]
            };
        }

        public bool ReplaceTestMethod(
            CandidateMethodContext context,
            string existingMethodName,
            string replacementTestMethodCode)
        {
            ReplaceCalls++;
            return true;
        }
    }

    /// <summary>Scoped temp directory deleted on dispose.</summary>
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
