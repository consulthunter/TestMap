using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.Rules.Generation;

public static class GenerationValidationDecisionEngine
{
    public static IReadOnlyList<RuleDecisionRecord> Evaluate(
        GeneratedTestExecutionResult execution,
        MetricsDrivenPath? metricsPath,
        bool coverageImproved,
        bool mutationImproved,
        bool mutantKilled,
        bool usefulMetricSignal)
    {
        var outcome = ResolveOutcome(execution);
        var failureClass = ResolveFailureClass(execution);
        var confidence = ResolveConfidence(execution, outcome, failureClass);
        var decisions = new List<RuleDecisionRecord>
        {
            Decision(execution.CodeExtracted ? GenerationValidationRuleDefinitions.CodeExtracted : GenerationValidationRuleDefinitions.CodeMissing, execution.CodeExtracted ? "CodeExtracted" : "CodeMissing"),
            Decision(execution.MethodNameExtracted ? GenerationValidationRuleDefinitions.MethodNameExtracted : GenerationValidationRuleDefinitions.MethodNameMissing, execution.MethodNameExtracted ? "MethodNameExtracted" : "MethodNameMissing"),
            Decision(execution.SyntaxValid ? GenerationValidationRuleDefinitions.SyntaxValid : GenerationValidationRuleDefinitions.SyntaxInvalid, execution.SyntaxValid ? "SyntaxValid" : "SyntaxInvalid"),
            Decision(execution.CompilationSucceeded ? GenerationValidationRuleDefinitions.CompilationSucceeded : GenerationValidationRuleDefinitions.CompilationFailed, execution.CompilationSucceeded ? "CompilationSucceeded" : "CompilationFailed"),
            Decision(execution.TestsExecuted ? GenerationValidationRuleDefinitions.TestsExecuted : GenerationValidationRuleDefinitions.TestsNotExecuted, execution.TestsExecuted ? "TestsExecuted" : "TestsNotExecuted"),
            Decision(execution.AllTestsPassed ? GenerationValidationRuleDefinitions.AllTestsPassed : GenerationValidationRuleDefinitions.TestFailure, execution.AllTestsPassed ? "AllTestsPassed" : "TestFailure"),
            Decision(coverageImproved ? GenerationValidationRuleDefinitions.CoverageImproved : GenerationValidationRuleDefinitions.CoverageDidNotImprove, coverageImproved ? "CoverageImproved" : "CoverageDidNotImprove",
                RuleDecisionFactory.CreateEvidence("Coverage", "Delta", execution.CoverageImprovement.ToString("R"))),
            Decision(
                GenerationValidationRuleDefinitions.OutcomeSelected,
                outcome.ToString(),
                RuleDecisionFactory.CreateEvidence("Execution", "CompilationSucceeded", execution.CompilationSucceeded.ToString()),
                RuleDecisionFactory.CreateEvidence("Execution", "TestsExecuted", execution.TestsExecuted.ToString()),
                RuleDecisionFactory.CreateEvidence("Execution", "AllTestsPassed", execution.AllTestsPassed.ToString())),
            Decision(
                GenerationValidationRuleDefinitions.ConfidenceSelected,
                confidence.ToString(),
                RuleDecisionFactory.CreateEvidence("Roslyn", "ValidationSkipped", execution.RoslynValidationSkipped.ToString()),
                RuleDecisionFactory.CreateEvidence("Roslyn", "ValidationSucceeded", execution.RoslynValidationSucceeded.ToString()),
                RuleDecisionFactory.CreateEvidence("Roslyn", "NewDiagnosticCount", execution.NewRoslynDiagnostics.Count.ToString())),
            Decision(
                GenerationValidationRuleDefinitions.DiagnosticComparison,
                "ModifiedTestFileDiagnosticsCompared",
                RuleDecisionFactory.CreateEvidence("Roslyn", "BeforeCount", execution.RoslynDiagnosticsBefore.Count.ToString()),
                RuleDecisionFactory.CreateEvidence("Roslyn", "AfterCount", execution.RoslynDiagnosticsAfter.Count.ToString()),
                RuleDecisionFactory.CreateEvidence("Roslyn", "NewCount", execution.NewRoslynDiagnostics.Count.ToString()),
                RuleDecisionFactory.CreateEvidence("Roslyn", "NewDiagnosticIds", string.Join(",", execution.NewRoslynDiagnostics.Select(x => x.Id).Distinct().Order()))),
        };

        if (failureClass.HasValue)
            decisions.Add(Decision(
                GenerationValidationRuleDefinitions.FailureClassSelected,
                failureClass.Value.ToString(),
                RuleDecisionFactory.CreateEvidence("Execution", "FailureStage", execution.FailureStage ?? string.Empty),
                RuleDecisionFactory.CreateEvidence("Execution", "FailureCategory", execution.FailureCategory ?? string.Empty),
                RuleDecisionFactory.CreateEvidence("Execution", "FailureKind", execution.FailureKind.ToString())));

        if (execution.NewRoslynDiagnostics.Any(IsInfrastructureDiagnostic) || execution.RoslynValidationSkipped)
            decisions.Add(Decision(
                GenerationValidationRuleDefinitions.DiagnosticInfrastructureNoise,
                execution.RoslynValidationSkipped ? "RoslynValidationSkipped" : "InfrastructureDiagnosticsObserved",
                RuleDecisionFactory.CreateEvidence("Roslyn", "DiagnosticIds", string.Join(",", execution.NewRoslynDiagnostics.Where(IsInfrastructureDiagnostic).Select(x => x.Id).Distinct().Order())),
                RuleDecisionFactory.CreateEvidence("Roslyn", "DecisionImpact", "Advisory")));

        if (failureClass is GenerationValidationFailureClass.MalformedGeneratedCode or GenerationValidationFailureClass.BadInsertion)
            decisions.Add(Decision(
                GenerationValidationRuleDefinitions.DiagnosticGeneratedCodeAttributed,
                failureClass.Value.ToString(),
                RuleDecisionFactory.CreateEvidence("Roslyn", "NewDiagnosticIds", string.Join(",", execution.NewRoslynDiagnostics.Select(x => x.Id).Distinct().Order())),
                RuleDecisionFactory.CreateEvidence("Decision", "DecisionImpact", "HardReject")));

        if (metricsPath is MetricsDrivenPath.Mutation or MetricsDrivenPath.CoverageAndMutation)
        {
            decisions.Add(Decision(
                mutationImproved || mutantKilled
                    ? GenerationValidationRuleDefinitions.MutationImproved
                    : GenerationValidationRuleDefinitions.MutationUnavailable,
                mutationImproved || mutantKilled ? "MutationImproved" : "MutationUnavailable",
                RuleDecisionFactory.CreateEvidence("Mutation", "Delta", execution.MutationScoreImprovement?.ToString("R") ?? string.Empty)));
        }
        else
        {
            decisions.Add(Decision(
                GenerationValidationRuleDefinitions.MutationUnavailable,
                "MutationNotApplicable",
                RuleDecisionFactory.CreateEvidence("GenerationApproach", "MetricsPath", metricsPath?.ToString() ?? string.Empty)));
        }

        if (usefulMetricSignal)
            decisions.Add(Decision(GenerationValidationRuleDefinitions.UsefulMetricSignal, "UsefulMetricSignal"));

        decisions.AddRange(execution.ApplicationRuleDecisions);
        decisions.AddRange(execution.RoslynPreBuildRuleDecisions);

        return decisions;
    }

    public static GenerationValidationOutcome ResolveOutcome(GeneratedTestExecutionResult execution)
    {
        if (execution.CompilationSucceeded && execution.TestsExecuted && execution.AllTestsPassed)
            return GenerationValidationOutcome.Passed;

        if (execution.RoslynValidationSkipped && !execution.CompilationSucceeded && !execution.TestsExecuted)
            return GenerationValidationOutcome.Inconclusive;

        return GenerationValidationOutcome.Failed;
    }

    public static GenerationValidationFailureClass? ResolveFailureClass(GeneratedTestExecutionResult execution)
    {
        if (execution.CompilationSucceeded && execution.TestsExecuted && execution.AllTestsPassed)
            return null;

        if (execution.FailureStage == "application" ||
            execution.FailureCategory is "generated_test_method_not_applied" or "generated_test_application_failed")
            return GenerationValidationFailureClass.BadInsertion;

        if (string.Equals(execution.FailureStage, "roslyn-pre-build", StringComparison.Ordinal) &&
            Enum.TryParse<GenerationValidationFailureClass>(execution.FailureCategory, out var preBuildFailureClass))
            return preBuildFailureClass;

        if (!execution.SyntaxValid || execution.NewRoslynDiagnostics.Any(IsSyntaxDiagnostic))
            return GenerationValidationFailureClass.MalformedGeneratedCode;

        if (execution.RoslynValidationSkipped || execution.NewRoslynDiagnostics.Any(IsInfrastructureDiagnostic))
            return GenerationValidationFailureClass.Infrastructure;

        return execution.FailureKind switch
        {
            Models.Experiment.TestFailureKind.Runtime => GenerationValidationFailureClass.Runtime,
            Models.Experiment.TestFailureKind.Assertion => GenerationValidationFailureClass.Assertion,
            Models.Experiment.TestFailureKind.Infrastructure => GenerationValidationFailureClass.Infrastructure,
            Models.Experiment.TestFailureKind.Compilation => GenerationValidationFailureClass.CompilerSemantic,
            _ => execution.CompilationSucceeded
                ? null
                : GenerationValidationFailureClass.CompilerSemantic
        };
    }

    public static GenerationValidationConfidence ResolveConfidence(
        GeneratedTestExecutionResult execution,
        GenerationValidationOutcome outcome,
        GenerationValidationFailureClass? failureClass)
    {
        if (outcome == GenerationValidationOutcome.Passed)
            return GenerationValidationConfidence.High;

        if (failureClass is GenerationValidationFailureClass.MalformedGeneratedCode or GenerationValidationFailureClass.BadInsertion)
            return GenerationValidationConfidence.High;

        if (string.Equals(execution.FailureStage, "roslyn-pre-build", StringComparison.Ordinal))
            return GenerationValidationConfidence.High;

        if (failureClass == GenerationValidationFailureClass.Infrastructure || execution.RoslynValidationSkipped)
            return GenerationValidationConfidence.Low;

        return GenerationValidationConfidence.Medium;
    }

    private static bool IsSyntaxDiagnostic(RoslynDiagnosticSnapshot diagnostic)
    {
        return diagnostic.Id.StartsWith("CS1", StringComparison.Ordinal) ||
               diagnostic.Id is "CS1513" or "CS1514" or "CS1519" or "CS1525" or "CS1002";
    }

    private static bool IsInfrastructureDiagnostic(RoslynDiagnosticSnapshot diagnostic)
    {
        if (diagnostic.Id is "CS0006" or "CS0518")
            return true;

        if ((diagnostic.Id is "CS0103" or "CS0246") &&
            IsKnownTestFrameworkSymbol(ExtractQuotedSymbolName(diagnostic.Message)))
            return true;

        return diagnostic.Message.Contains("predefined type", StringComparison.OrdinalIgnoreCase) ||
               diagnostic.Message.Contains("metadata file", StringComparison.OrdinalIgnoreCase) ||
               diagnostic.Message.Contains("reference", StringComparison.OrdinalIgnoreCase) ||
               diagnostic.Message.Contains("assembly", StringComparison.OrdinalIgnoreCase) ||
               diagnostic.Message.Contains("assets file", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractQuotedSymbolName(string message)
    {
        var start = message.IndexOf('\'');
        if (start < 0) return null;

        var end = message.IndexOf('\'', start + 1);
        return end <= start + 1 ? null : message.Substring(start + 1, end - start - 1);
    }

    private static bool IsKnownTestFrameworkSymbol(string? symbolName)
    {
        if (string.IsNullOrWhiteSpace(symbolName)) return false;

        var normalized = symbolName.EndsWith("Attribute", StringComparison.Ordinal)
            ? symbolName[..^"Attribute".Length]
            : symbolName;

        return normalized is
            "Assert" or
            "Fact" or
            "Theory" or
            "InlineData" or
            "MemberData" or
            "ClassData" or
            "Trait" or
            "Test" or
            "TestCase" or
            "TestCaseSource" or
            "TestMethod" or
            "DataTestMethod" or
            "DataRow" or
            "TestSubject";
    }

    private static RuleDecisionRecord Decision(RuleDefinition rule, string value, params RuleEvidenceRecord[] evidence)
    {
        return RuleDecisionFactory.CreateDecision(
            "GenerationValidation",
            value,
            rule,
            RuleConfidence.High,
            evidence,
            rule.Description);
    }
}
