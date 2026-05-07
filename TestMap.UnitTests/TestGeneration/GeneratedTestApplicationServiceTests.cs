using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GeneratedTestApplicationServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ApplyAsync_DelegatesToConfiguredExecutor()
    {
        var executor = new FakeActionExecutor(TestActionExecutorMode.ActionAware);
        var service = new GeneratedTestApplicationService([executor]);
        var context = CreateCandidateContext();

        var result = await service.ApplyAsync(
            context,
            "generated test",
            "Generated_Test",
            TestActionExecutorMode.ActionAware);

        Assert.True(result.Success);
        Assert.Equal(context.TestFilePath, result.AppliedFilePath);
        Assert.Equal("Generated_Test", result.AppliedTestMethodName);
        Assert.Equal(CandidateActionKind.ExtendExistingTestSuite, result.ActionKind);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ApplyAsync_ThrowsWhenExecutorModeIsMissing()
    {
        var service = new GeneratedTestApplicationService([]);
        var context = CreateCandidateContext();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyAsync(
                context,
                "generated test",
                "Generated_Test",
                TestActionExecutorMode.ActionAware));

        Assert.Contains("No test action executor is registered", exception.Message);
    }

    private static CandidateMethodContext CreateCandidateContext()
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
            TestNamespace = "Sample.Tests",
            TestClassName = "CalculatorTests",
            TestFilePath = "CalculatorTests.cs",
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
            ExampleTest = "[Fact] public void Existing() { }",
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = "public class CalculatorTests { }",
            TestFileContents = "public class CalculatorTests { }",
            TestSupportContext = string.Empty,
            TestFramework = "xUnit",
            TestDependencies = "using Xunit;",
            CoverageGapSummary = string.Empty
        };
    }

    private sealed class FakeActionExecutor : ITestActionExecutor
    {
        public FakeActionExecutor(TestActionExecutorMode mode)
        {
            Mode = mode;
        }

        public TestActionExecutorMode Mode { get; }
        public int CallCount { get; private set; }

        public Task<TestActionExecutionResult> ExecuteAsync(
            CandidateMethodContext context,
            string generatedTest,
            string? generatedTestMethodName,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new TestActionExecutionResult
            {
                Success = true,
                AppliedFilePath = context.TestFilePath,
                AppliedTestMethodName = generatedTestMethodName,
                ActionKind = CandidateActionKind.ExtendExistingTestSuite
            });
        }
    }
}
