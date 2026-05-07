using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Services.TestGeneration.Editing;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.TestGeneration;

public sealed class BasicExtensionTestActionExecutorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_AppendsEvenWhenCandidateRecommendsImprovingExistingTest()
    {
        var editor = new FakeTestCodeEditService();
        var executor = new BasicExtensionTestActionExecutor(editor);
        var context = new CandidateMethodContext
        {
            Method = new CandidateMethod
            {
                MethodName = "Calculate",
                RecommendedAction = CandidateActionKind.ImproveExistingTest,
                ExistingTestMethodName = "Calculate_Existing"
            },
            MethodSignature = "int Calculate()",
            ContainingClass = "Calculator",
            TestNamespace = "Sample.Tests",
            TestClassName = "CalculatorTests",
            TestFilePath = "Tests.cs",
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
            SolutionFilePath = "Sample.sln",
            ExampleTest = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = "public class CalculatorTests { }",
            TestFileContents = "public class CalculatorTests { }",
            TestSupportContext = string.Empty,
            TestFramework = "xUnit",
            TestDependencies = "using Xunit;",
            CoverageGapSummary = string.Empty
        };

        var result = await executor.ExecuteAsync(
            context,
            "[Fact] public void Calculate_NewCoverage() { }",
            "Calculate_NewCoverage");

        Assert.True(result.Success);
        Assert.Equal(CandidateActionKind.ExtendExistingTestSuite, result.ActionKind);
        Assert.Equal(1, editor.AppendCalls);
        Assert.Equal(0, editor.ReplaceCalls);
        Assert.Contains(result.RuleDecisions, x => x.Value == "AppendTargetSelected");
    }

    private sealed class FakeTestCodeEditService : ITestCodeEditService
    {
        public int AppendCalls { get; private set; }
        public int ReplaceCalls { get; private set; }

        public bool EnsureTestClassExists(CandidateMethodContext context)
        {
            return true;
        }

        public bool AppendTestMethod(CandidateMethodContext context, string testMethodCode)
        {
            AppendCalls++;
            return true;
        }

        public TestMethodAppendResult AppendTestMethodWithResult(CandidateMethodContext context, string testMethodCode)
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
}
