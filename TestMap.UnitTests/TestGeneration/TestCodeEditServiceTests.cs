using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.Editing;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.TestGeneration;

public sealed class TestCodeEditServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void AppendTestMethodWithResult_AppendsGeneratedMethodInsideExpectedClass()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var testFilePath = Path.Combine(tempDirectory, "CalculatorTests.cs");
            File.WriteAllText(
                testFilePath,
                """
                namespace Demo.Tests;

                public sealed class CalculatorTests
                {
                    public void Existing()
                    {
                    }
                }

                public sealed class OtherTests
                {
                }
                """);
            var service = new TestCodeEditService();

            var result = service.AppendTestMethodWithResult(
                CreateContext(testFilePath),
                """
                [Fact]
                public void Add_ReturnsSum()
                {
                    Assert.Equal(3, 1 + 2);
                }
                """);

            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(testFilePath)).GetCompilationUnitRoot();
            var targetClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(x => x.Identifier.Text == "CalculatorTests");
            var otherClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Single(x => x.Identifier.Text == "OtherTests");

            Assert.True(result.Success);
            Assert.Contains(targetClass.Members.OfType<MethodDeclarationSyntax>(), x => x.Identifier.Text == "Add_ReturnsSum");
            Assert.DoesNotContain(otherClass.Members.OfType<MethodDeclarationSyntax>(), x => x.Identifier.Text == "Add_ReturnsSum");
            Assert.Contains(result.RuleDecisions, x => x.Value == "AppendTargetSelected");
            Assert.Contains(result.RuleDecisions, x => x.Value == "GeneratedMethodInserted");
            Assert.Contains(result.RuleDecisions.SelectMany(x => x.Evidence), x =>
                x.Key == "InsideExpectedObject" && x.Value == "True");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AppendTestMethodWithResult_ReportsParseFailureForMalformedGeneratedMethod()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "TestMap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var testFilePath = Path.Combine(tempDirectory, "CalculatorTests.cs");
            File.WriteAllText(
                testFilePath,
                """
                public sealed class CalculatorTests
                {
                }
                """);
            var service = new TestCodeEditService();

            var result = service.AppendTestMethodWithResult(
                CreateContext(testFilePath),
                "this is not a method");

            Assert.False(result.Success);
            Assert.Contains(result.RuleDecisions, x => x.Value == "GeneratedMethodParseFailed");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static CandidateMethodContext CreateContext(string testFilePath)
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
            ContainingClass = "Calculator",
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
}
