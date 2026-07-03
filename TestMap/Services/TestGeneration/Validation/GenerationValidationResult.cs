using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Rules;

namespace TestMap.Services.TestGeneration.Validation;

public sealed class GenerationValidationResult
{
    public GenerationValidationOutcome Outcome { get; init; } = GenerationValidationOutcome.Inconclusive;
    public GenerationValidationConfidence Confidence { get; init; } = GenerationValidationConfidence.Low;
    public GenerationValidationFailureClass? FailureClass { get; init; }
    public bool CodeExtracted { get; init; }
    public bool MethodNameExtracted { get; init; }
    public bool SyntaxValid { get; init; }
    public bool CompilationSucceeded { get; init; }
    public bool TestsExecuted { get; init; }
    public bool AllTestsPassed { get; init; }
    public bool CoverageImproved { get; init; }
    public bool MutationScoreImproved { get; init; }
    public bool MutantKilled { get; init; }
    public bool HasUsefulMetricSignal { get; init; }
    public double CoverageImprovement { get; init; }
    public double? MutationScoreImprovement { get; init; }
    public MetricsDrivenPath? MetricsPath { get; init; }
    public string? FailureStage { get; init; }
    public string? FailureCategory { get; init; }
    public string? FailureSummary { get; init; }
    public bool RoslynValidationSucceeded { get; init; }
    public bool RoslynValidationSkipped { get; init; }
    public IReadOnlyList<RoslynDiagnosticSnapshot> RoslynDiagnosticsBefore { get; init; } = [];
    public IReadOnlyList<RoslynDiagnosticSnapshot> RoslynDiagnosticsAfter { get; init; } = [];
    public IReadOnlyList<RoslynDiagnosticSnapshot> NewRoslynDiagnostics { get; init; } = [];
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
