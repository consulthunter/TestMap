using Microsoft.CodeAnalysis;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Rules.Generation;

namespace TestMap.Services.TestGeneration.Validation;

public static class RoslynPreBuildDecisionClassifier
{
    public static RoslynPreBuildDecision Classify(RoslynGeneratedTestValidationResult validation)
    {
        if (validation.Skipped)
            return Allow(
                "Roslyn validation was skipped; real build remains the authoritative check.",
                GenerationValidationConfidence.Low,
                RuleDecisionFactory.CreateEvidence("Roslyn", "SkipReason", validation.SkipReason ?? string.Empty));

        var newErrors = validation.NewDiagnostics
            .Where(x => string.Equals(x.Severity, DiagnosticSeverity.Error.ToString(), StringComparison.Ordinal))
            .ToList();

        if (newErrors.Count == 0)
            return Allow(
                "Roslyn reported no new error diagnostics in the modified test file.",
                GenerationValidationConfidence.High,
                DiagnosticEvidence(validation));

        var syntaxErrors = newErrors.Where(IsSyntaxDiagnostic).ToList();
        if (syntaxErrors.Count > 0)
            return Skip(
                "Roslyn reported high-confidence syntax errors local to the generated/modified test file.",
                GenerationValidationFailureClass.MalformedGeneratedCode,
                DiagnosticEvidence(validation, syntaxErrors));

        var infrastructureErrors = newErrors.Where(IsInfrastructureDiagnostic).ToList();
        if (infrastructureErrors.Count == newErrors.Count)
            return Allow(
                "Roslyn errors were classified as infrastructure/design-time diagnostics; real build remains authoritative.",
                GenerationValidationConfidence.Low,
                DiagnosticEvidence(validation, infrastructureErrors));

        var generatedSemanticErrors = newErrors
            .Where(x => !IsInfrastructureDiagnostic(x))
            .ToList();
        if (generatedSemanticErrors.Count > 0)
            return Skip(
                "Roslyn reported high-confidence generated-code-local semantic errors before build.",
                GenerationValidationFailureClass.CompilerSemantic,
                DiagnosticEvidence(validation, generatedSemanticErrors));

        return Allow(
            "Roslyn diagnostics were ambiguous; real build remains authoritative.",
            GenerationValidationConfidence.Low,
            DiagnosticEvidence(validation));
    }

    private static RoslynPreBuildDecision Allow(
        string reason,
        GenerationValidationConfidence confidence,
        params RuleEvidenceRecord[] evidence)
    {
        return new RoslynPreBuildDecision
        {
            ShouldBuild = true,
            Confidence = confidence,
            Reason = reason,
            RuleDecisions =
            [
                RuleDecisionFactory.CreateDecision(
                    "RoslynPreBuild",
                    "BuildAllowed",
                    GenerationValidationRuleDefinitions.PreBuildAllowed,
                    confidence == GenerationValidationConfidence.High ? RuleConfidence.High : RuleConfidence.Low,
                    evidence,
                    reason)
            ]
        };
    }

    private static RoslynPreBuildDecision Skip(
        string reason,
        GenerationValidationFailureClass failureClass,
        params RuleEvidenceRecord[] evidence)
    {
        return new RoslynPreBuildDecision
        {
            ShouldBuild = false,
            Confidence = GenerationValidationConfidence.High,
            FailureClass = failureClass,
            Reason = reason,
            RuleDecisions =
            [
                RuleDecisionFactory.CreateDecision(
                    "RoslynPreBuild",
                    "BuildSkipped",
                    GenerationValidationRuleDefinitions.PreBuildSkipped,
                    RuleConfidence.High,
                    evidence,
                    reason)
            ]
        };
    }

    private static RuleEvidenceRecord[] DiagnosticEvidence(
        RoslynGeneratedTestValidationResult validation,
        IReadOnlyList<RoslynDiagnosticSnapshot>? selectedDiagnostics = null)
    {
        var diagnostics = selectedDiagnostics ?? validation.NewDiagnostics;
        return
        [
            RuleDecisionFactory.CreateEvidence("Roslyn", "BeforeCount", validation.Before.Diagnostics.Count.ToString()),
            RuleDecisionFactory.CreateEvidence("Roslyn", "AfterCount", validation.After.Diagnostics.Count.ToString()),
            RuleDecisionFactory.CreateEvidence("Roslyn", "NewCount", validation.NewDiagnostics.Count.ToString()),
            RuleDecisionFactory.CreateEvidence("Roslyn", "SelectedDiagnosticIds", string.Join(",", diagnostics.Select(x => x.Id).Distinct().Order())),
            RuleDecisionFactory.CreateEvidence("Roslyn", "SelectedDiagnosticSeverities", string.Join(",", diagnostics.Select(x => x.Severity).Distinct().Order())),
            RuleDecisionFactory.CreateEvidence("Roslyn", "SelectedDiagnosticLocations", string.Join(";", diagnostics.Select(FormatLocation).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(5)))
        ];
    }

    private static string FormatLocation(RoslynDiagnosticSnapshot diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic.FilePath)) return string.Empty;

        return $"{diagnostic.FilePath}({diagnostic.StartLine + 1},{diagnostic.StartColumn + 1})";
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
}
