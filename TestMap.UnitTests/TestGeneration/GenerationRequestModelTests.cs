using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Services.TestGeneration;
using TestMap.Services.TestGeneration.Evidence;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GenerationRequestModelTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void TestGenerationRequest_DefaultsToConfiguredPipelineControls()
    {
        var request = CreateGenerationRequest();

        Assert.Equal(TestGenerationObjective.TestSuiteExpansion, request.Objective);
        Assert.Equal(TestGenerationApproach.MetricsDriven, request.Approach);
        Assert.Null(request.MetricsPath);
        Assert.Equal(GenerationContextMode.ChainedHistory, request.ContextMode);
        Assert.NotNull(request.Steps);
        Assert.True(request.Steps.EnableEvidencePackage);
        Assert.False(request.Steps.EnableContextGraph);
        Assert.True(request.Steps.EnableScenario);
        Assert.Null(request.ExperimentVariantId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TestRepairRequest_DefaultsToConfiguredPipelineControls()
    {
        var request = new TestRepairRequest
        {
            MethodBody = "public int Add(int x, int y) => x + y;",
            MethodName = "Add",
            GeneratedTest = "[Fact] public void Add_ReturnsSum() { }",
            TestClass = "public class CalculatorTests { }",
            TestFramework = "xUnit",
            TestDependencies = "using Xunit;",
            TestFileContents = "public class CalculatorTests { }",
            TestSupportContext = string.Empty,
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            CoverageGapSummary = string.Empty,
            ErrorLogs = string.Empty,
            Provider = AiProvider.OpenAi
        };

        Assert.Equal(TestGenerationObjective.TestSuiteExpansion, request.Objective);
        Assert.Equal(TestGenerationApproach.MetricsDriven, request.Approach);
        Assert.Null(request.MetricsPath);
        Assert.Equal(GenerationContextMode.ChainedHistory, request.ContextMode);
        Assert.NotNull(request.Steps);
        Assert.True(request.Steps.EnableFinalTest);
        Assert.Null(request.ExperimentVariantId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GenerationStepMetadata_DefaultStatusIsExecuted()
    {
        var metadata = new GenerationStepMetadata
        {
            StepType = GenerationStepType.Scenario,
            Prompt = "prompt",
            Response = "response"
        };

        Assert.Equal(GenerationStepStatus.Executed, metadata.Status);
        Assert.Null(metadata.SkipReason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GenerationEvidencePackage_CarriesPipelineControlsAndRuleDecisions()
    {
        var ruleDecision = new RuleDecisionRecord
        {
            DecisionKind = "Evidence",
            Value = "Included",
            RuleId = "generation.evidence.context",
            RuleVersion = "1.0",
            Confidence = RuleConfidence.High
        };
        var candidateContext = CreateCandidateContext();

        var package = new GenerationEvidencePackage
        {
            Objective = TestGenerationObjective.TestSuiteExpansion,
            Approach = TestGenerationApproach.MetricsDriven,
            MetricsPath = MetricsDrivenPath.CoverageAndMutation,
            CandidateContext = candidateContext,
            StrategyInstruction = "Use explicit coverage and mutation evidence.",
            Coverage = new CoverageEvidence
            {
                CurrentLineCoverage = 0.42,
                Summary = "Coverage gaps are available.",
                Gaps =
                [
                    new CoverageGapEvidence
                    {
                        LineNumber = 12,
                        GapKind = "UncoveredLine",
                        SourceText = "return false;"
                    }
                ]
            },
            Mutation = new MutationEvidence
            {
                BaselineMutationScore = 0.55,
                Summary = "Surviving mutants are available.",
                SurvivingMutants =
                [
                    new SurvivingMutantEvidence
                    {
                        MutantId = "M1",
                        MutatorName = "EqualityOperator",
                        OriginalCode = ">",
                        ReplacementCode = ">="
                    }
                ]
            },
            RuleDecisions = [ruleDecision]
        };

        Assert.Equal(TestGenerationApproach.MetricsDriven, package.Approach);
        Assert.Equal(MetricsDrivenPath.CoverageAndMutation, package.MetricsPath);
        Assert.Same(candidateContext, package.CandidateContext);
        Assert.Equal(0.42, package.Coverage?.CurrentLineCoverage);
        Assert.Single(package.Coverage?.Gaps ?? []);
        Assert.Equal(0.55, package.Mutation?.BaselineMutationScore);
        Assert.Single(package.Mutation?.SurvivingMutants ?? []);
        Assert.Single(package.RuleDecisions);
    }

    private static TestGenerationRequest CreateGenerationRequest()
    {
        return new TestGenerationRequest
        {
            MethodBody = "public int Add(int x, int y) => x + y;",
            MethodName = "Add",
            MethodSignature = "public int Add(int x, int y)",
            ContainingClass = "public class Calculator { }",
            ExampleTest = "[Fact] public void Existing() { }",
            ExampleTestMetadataSummary = string.Empty,
            ProjectTestMetadataSummary = string.Empty,
            TestClass = "public class CalculatorTests { }",
            TestFileContents = "public class CalculatorTests { }",
            TestSupportContext = string.Empty,
            TestFramework = "xUnit",
            TestDependencies = "using Xunit;",
            CoverageGapSummary = string.Empty,
            Provider = AiProvider.OpenAi
        };
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
}
