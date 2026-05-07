using TestMap.Rules.Generation;
using TestMap.Services.TestGeneration.Evidence;
using TestMap.Services.TestGeneration.Execution;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Validation;

public sealed class GenerationValidationService : IGenerationValidationService
{
    public GenerationValidationResult Validate(
        GeneratedTestExecutionResult execution,
        CandidateMethodContext context,
        GenerationEvidencePackage evidence)
    {
        var coverageImproved = execution.CoverageImprovement > 0;
        var mutationImproved = execution.MutationScoreImprovement is > 0;
        var mutantKilled = mutationImproved;
        var usefulMetricSignal = coverageImproved || mutationImproved || mutantKilled;

        var decisions = GenerationValidationDecisionEngine.Evaluate(
            execution,
            evidence.MetricsPath,
            coverageImproved,
            mutationImproved,
            mutantKilled,
            usefulMetricSignal);
        var outcome = GenerationValidationDecisionEngine.ResolveOutcome(execution);
        var failureClass = GenerationValidationDecisionEngine.ResolveFailureClass(execution);
        var confidence = GenerationValidationDecisionEngine.ResolveConfidence(execution, outcome, failureClass);

        return new GenerationValidationResult
        {
            Outcome = outcome,
            Confidence = confidence,
            FailureClass = failureClass,
            CodeExtracted = execution.CodeExtracted,
            MethodNameExtracted = execution.MethodNameExtracted,
            SyntaxValid = execution.SyntaxValid,
            CompilationSucceeded = execution.CompilationSucceeded,
            TestsExecuted = execution.TestsExecuted,
            AllTestsPassed = execution.AllTestsPassed,
            CoverageImproved = coverageImproved,
            MutationScoreImproved = mutationImproved,
            MutantKilled = mutantKilled,
            HasUsefulMetricSignal = usefulMetricSignal,
            CoverageImprovement = execution.CoverageImprovement,
            MutationScoreImprovement = execution.MutationScoreImprovement,
            MetricsPath = evidence.MetricsPath,
            FailureStage = execution.FailureStage,
            FailureCategory = execution.FailureCategory,
            FailureSummary = execution.FailureSummary,
            RoslynValidationSucceeded = execution.RoslynValidationSucceeded,
            RoslynValidationSkipped = execution.RoslynValidationSkipped,
            RoslynDiagnosticsBefore = execution.RoslynDiagnosticsBefore,
            RoslynDiagnosticsAfter = execution.RoslynDiagnosticsAfter,
            NewRoslynDiagnostics = execution.NewRoslynDiagnostics,
            RuleDecisions = decisions
        };
    }
}
