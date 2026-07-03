using TestMap.Rules.Generation;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.UnitTests.TestGeneration;

public sealed class RoslynPreBuildDecisionClassifierTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_AllowsBuildWhenRoslynValidationWasSkipped()
    {
        var decision = RoslynPreBuildDecisionClassifier.Classify(
            RoslynGeneratedTestValidationResult.Skip("Roslyn disabled."));

        Assert.True(decision.ShouldBuild);
        Assert.Equal(GenerationValidationConfidence.Low, decision.Confidence);
        Assert.Contains(decision.RuleDecisions, x =>
            x.RuleId == GenerationValidationRuleDefinitions.PreBuildAllowed.Id &&
            x.Value == "BuildAllowed");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_AllowsBuildWhenNoNewRoslynErrorsExist()
    {
        var decision = RoslynPreBuildDecisionClassifier.Classify(ValidationWithNewDiagnostics(
            new RoslynDiagnosticSnapshot
            {
                Id = "CS0168",
                Severity = "Warning",
                Message = "The variable 'unused' is declared but never used.",
                FilePath = "CalculatorTests.cs"
            }));

        Assert.True(decision.ShouldBuild);
        Assert.Equal(GenerationValidationConfidence.High, decision.Confidence);
        Assert.Contains(decision.RuleDecisions, x =>
            x.RuleId == GenerationValidationRuleDefinitions.PreBuildAllowed.Id &&
            x.Evidence.Any(e => e.Key == "NewCount" && e.Value == "1"));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("CS0518", "Predefined type 'System.Object' is not defined or imported.")]
    [InlineData("CS0103", "The name 'Assert' does not exist in the current context.")]
    [InlineData("CS0246", "The type or namespace name 'FactAttribute' could not be found.")]
    public void Classify_AllowsBuildForInfrastructureOrTestFrameworkDesignTimeErrors(
        string diagnosticId,
        string message)
    {
        var decision = RoslynPreBuildDecisionClassifier.Classify(ValidationWithNewDiagnostics(
            new RoslynDiagnosticSnapshot
            {
                Id = diagnosticId,
                Severity = "Error",
                Message = message,
                FilePath = "CalculatorTests.cs"
            }));

        Assert.True(decision.ShouldBuild);
        Assert.Equal(GenerationValidationConfidence.Low, decision.Confidence);
        Assert.Contains(decision.RuleDecisions, x =>
            x.RuleId == GenerationValidationRuleDefinitions.PreBuildAllowed.Id &&
            x.Evidence.Any(e => e.Key == "SelectedDiagnosticIds" && e.Value == diagnosticId));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_SkipsBuildForMalformedGeneratedCode()
    {
        var decision = RoslynPreBuildDecisionClassifier.Classify(ValidationWithNewDiagnostics(
            new RoslynDiagnosticSnapshot
            {
                Id = "CS1002",
                Severity = "Error",
                Message = "; expected",
                FilePath = "CalculatorTests.cs"
            }));

        Assert.False(decision.ShouldBuild);
        Assert.Equal(GenerationValidationConfidence.High, decision.Confidence);
        Assert.Equal(GenerationValidationFailureClass.MalformedGeneratedCode, decision.FailureClass);
        Assert.Contains(decision.RuleDecisions, x =>
            x.RuleId == GenerationValidationRuleDefinitions.PreBuildSkipped.Id &&
            x.Value == "BuildSkipped");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_SkipsBuildForGeneratedCodeLocalSemanticErrors()
    {
        var decision = RoslynPreBuildDecisionClassifier.Classify(ValidationWithNewDiagnostics(
            new RoslynDiagnosticSnapshot
            {
                Id = "CS0246",
                Severity = "Error",
                Message = "The type or namespace name 'MissingProductionType' could not be found.",
                FilePath = "CalculatorTests.cs"
            }));

        Assert.False(decision.ShouldBuild);
        Assert.Equal(GenerationValidationConfidence.High, decision.Confidence);
        Assert.Equal(GenerationValidationFailureClass.CompilerSemantic, decision.FailureClass);
        Assert.Contains(decision.RuleDecisions.SelectMany(x => x.Evidence), x =>
            x.Key == "SelectedDiagnosticIds" && x.Value == "CS0246");
    }

    private static RoslynGeneratedTestValidationResult ValidationWithNewDiagnostics(
        params RoslynDiagnosticSnapshot[] diagnostics)
    {
        return new RoslynGeneratedTestValidationResult
        {
            Succeeded = diagnostics.All(x => x.Severity != "Error"),
            Before = new RoslynGeneratedTestDiagnosticSnapshot
            {
                Captured = true,
                DocumentPath = "CalculatorTests.cs",
                Diagnostics = []
            },
            After = new RoslynGeneratedTestDiagnosticSnapshot
            {
                Captured = true,
                DocumentPath = "CalculatorTests.cs",
                Diagnostics = diagnostics
            },
            NewDiagnostics = diagnostics
        };
    }
}
