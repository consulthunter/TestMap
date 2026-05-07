using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Rules.Generation;
using TestMap.Services.TestGeneration.Acceptance;
using TestMap.Services.TestGeneration.Classification;
using TestMap.Services.TestGeneration.Evidence;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.UnitTests.TestGeneration;

public sealed class GenerationDecisionServicesTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Validation_RecordsCompilationPassingAndCoverageSignal()
    {
        var service = new GenerationValidationService();

        var result = service.Validate(
            new GeneratedTestExecutionResult
            {
                CodeExtracted = true,
                MethodNameExtracted = true,
                SyntaxValid = true,
                CompilationSucceeded = true,
                TestsExecuted = true,
                AllTestsPassed = true,
                CoverageImprovement = 0.12
            },
            null!,
            new GenerationEvidencePackage
            {
                MetricsPath = MetricsDrivenPath.Coverage
            });

        Assert.True(result.CodeExtracted);
        Assert.True(result.CompilationSucceeded);
        Assert.True(result.AllTestsPassed);
        Assert.True(result.CoverageImproved);
        Assert.True(result.HasUsefulMetricSignal);
        Assert.Equal(GenerationValidationOutcome.Passed, result.Outcome);
        Assert.Equal(GenerationValidationConfidence.High, result.Confidence);
        Assert.Null(result.FailureClass);
        Assert.Contains(result.RuleDecisions, x => x.Value == "CoverageImproved");
        Assert.Contains(result.RuleDecisions, x => x.Value == "Passed");
        Assert.Contains(result.RuleDecisions, x => x.Value == "High");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validation_ClassifiesRoslynInfrastructureDiagnosticsAsLowConfidence()
    {
        var service = new GenerationValidationService();

        var result = service.Validate(
            new GeneratedTestExecutionResult
            {
                CodeExtracted = true,
                MethodNameExtracted = true,
                SyntaxValid = true,
                CompilationSucceeded = false,
                TestsExecuted = false,
                AllTestsPassed = false,
                RoslynValidationSucceeded = false,
                RoslynValidationSkipped = false,
                NewRoslynDiagnostics =
                [
                    new RoslynDiagnosticSnapshot
                    {
                        Id = "CS0518",
                        Severity = "Error",
                        Message = "Predefined type 'System.Object' is not defined or imported.",
                        FilePath = "CalculatorTests.cs"
                    }
                ],
                FailureKind = TestFailureKind.Compilation
            },
            null!,
            new GenerationEvidencePackage
            {
                MetricsPath = MetricsDrivenPath.Coverage
            });

        Assert.Equal(GenerationValidationOutcome.Failed, result.Outcome);
        Assert.Equal(GenerationValidationConfidence.Low, result.Confidence);
        Assert.Equal(GenerationValidationFailureClass.Infrastructure, result.FailureClass);
        Assert.Contains(result.RuleDecisions, x => x.Value == "Infrastructure");
        Assert.Contains(result.RuleDecisions, x => x.Value == "InfrastructureDiagnosticsObserved");
        Assert.Contains(result.RuleDecisions.SelectMany(x => x.Evidence), x =>
            x.Key == "DecisionImpact" && x.Value == "Advisory");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validation_ClassifiesApplicationFailureAsHighConfidenceBadInsertion()
    {
        var service = new GenerationValidationService();

        var result = service.Validate(
            new GeneratedTestExecutionResult
            {
                CodeExtracted = true,
                MethodNameExtracted = true,
                SyntaxValid = true,
                ApplicationSucceeded = false,
                CompilationSucceeded = false,
                FailureStage = "application",
                FailureCategory = "generated_test_method_not_applied",
                FailureKind = TestFailureKind.Generation
            },
            null!,
            new GenerationEvidencePackage
            {
                MetricsPath = MetricsDrivenPath.Coverage
            });

        Assert.Equal(GenerationValidationOutcome.Failed, result.Outcome);
        Assert.Equal(GenerationValidationConfidence.High, result.Confidence);
        Assert.Equal(GenerationValidationFailureClass.BadInsertion, result.FailureClass);
        Assert.Contains(result.RuleDecisions, x => x.Value == "BadInsertion");
        Assert.Contains(result.RuleDecisions.SelectMany(x => x.Evidence), x =>
            x.Key == "DecisionImpact" && x.Value == "HardReject");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validation_CarriesRoslynPreBuildGateDecisionsIntoRuleDecisions()
    {
        var service = new GenerationValidationService();
        var preBuildDecision = RuleDecisionFactory.CreateDecision(
            "RoslynPreBuild",
            "BuildSkipped",
            GenerationValidationRuleDefinitions.PreBuildSkipped,
            RuleConfidence.High,
            [RuleDecisionFactory.CreateEvidence("Roslyn", "SelectedDiagnosticIds", "CS1002")],
            "Roslyn reported high-confidence syntax errors local to the generated/modified test file.");

        var result = service.Validate(
            new GeneratedTestExecutionResult
            {
                CodeExtracted = true,
                MethodNameExtracted = true,
                SyntaxValid = true,
                CompilationSucceeded = false,
                TestsExecuted = false,
                AllTestsPassed = false,
                RoslynValidationSucceeded = false,
                FailureKind = TestFailureKind.Compilation,
                FailureStage = "roslyn-pre-build",
                FailureCategory = GenerationValidationFailureClass.MalformedGeneratedCode.ToString(),
                RoslynPreBuildRuleDecisions = [preBuildDecision]
            },
            null!,
            new GenerationEvidencePackage
            {
                MetricsPath = MetricsDrivenPath.Coverage
            });

        Assert.Equal(GenerationValidationOutcome.Failed, result.Outcome);
        Assert.Equal(GenerationValidationConfidence.High, result.Confidence);
        Assert.Equal(GenerationValidationFailureClass.MalformedGeneratedCode, result.FailureClass);
        Assert.Contains(result.RuleDecisions, x =>
            x.RuleId == GenerationValidationRuleDefinitions.PreBuildSkipped.Id &&
            x.Value == "BuildSkipped");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Acceptance_RejectsWhenRequiredCoverageDeltaIsMissing()
    {
        var service = new GenerationAcceptanceService();

        var result = service.Evaluate(
            new GenerationValidationResult
            {
                CompilationSucceeded = true,
                TestsExecuted = true,
                AllTestsPassed = true,
                CoverageImproved = true,
                CoverageImprovement = 0.01
            },
            new TestAcceptanceConfig
            {
                RequireCompilationSuccess = true,
                RequireTestsToRun = true,
                RequireAllTestsPass = true,
                RequireCoverageImprovement = true,
                MinCoverageImprovement = 0.05
            });

        Assert.False(result.Accepted);
        Assert.Equal("Coverage did not improve enough.", result.Reason);
        Assert.Contains(result.RuleDecisions, x => x.Value == "RejectedMinCoverageDelta");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Acceptance_AcceptsWhenConfiguredRequirementsPass()
    {
        var service = new GenerationAcceptanceService();

        var result = service.Evaluate(
            new GenerationValidationResult
            {
                CompilationSucceeded = true,
                TestsExecuted = true,
                AllTestsPassed = true,
                CoverageImproved = true,
                CoverageImprovement = 0.06
            },
            new TestAcceptanceConfig
            {
                RequireCompilationSuccess = true,
                RequireTestsToRun = true,
                RequireAllTestsPass = true,
                RequireCoverageImprovement = true,
                MinCoverageImprovement = 0.05
            });

        Assert.True(result.Accepted);
        Assert.Equal("Accepted.", result.Reason);
        Assert.DoesNotContain(result.RuleDecisions, x => x.Value.StartsWith("Rejected", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(false, false, false, false, GeneratedTestClassification.ValidationFailed)]
    [InlineData(true, true, true, true, GeneratedTestClassification.ValidatedEvidencePositive)]
    [InlineData(true, true, false, true, GeneratedTestClassification.FailedEvidencePositive)]
    [InlineData(true, true, true, false, GeneratedTestClassification.ValidatedLowImpact)]
    [InlineData(true, false, false, false, GeneratedTestClassification.ValidationFailed)]
    public void Classification_MapsValidationFactsToGeneratedTestClassification(
        bool compiled,
        bool testsExecuted,
        bool passed,
        bool metricSignal,
        GeneratedTestClassification expected)
    {
        var service = new GenerationClassificationService();

        var result = service.Classify(new GenerationValidationResult
        {
            CompilationSucceeded = compiled,
            TestsExecuted = testsExecuted,
            AllTestsPassed = passed,
            HasUsefulMetricSignal = metricSignal
        });

        Assert.Equal(expected, result.Classification);
        Assert.Single(result.RuleDecisions);
    }
}
