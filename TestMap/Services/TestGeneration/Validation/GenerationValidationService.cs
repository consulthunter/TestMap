using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TestMap.Models.Rules;
using TestMap.Rules;
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
        var accessStrategyDecision = EvaluateAccessStrategy(execution, context);
        if (accessStrategyDecision != null)
            decisions = decisions.Concat([accessStrategyDecision]).ToList();
        var outcome = GenerationValidationDecisionEngine.ResolveOutcome(execution);
        var failureClass = GenerationValidationDecisionEngine.ResolveFailureClass(execution);
        var confidence = GenerationValidationDecisionEngine.ResolveConfidence(execution, outcome, failureClass);
        if (accessStrategyDecision?.RuleId == GenerationValidationRuleDefinitions.AccessStrategyViolated.Id)
        {
            outcome = GenerationValidationOutcome.Failed;
            failureClass = GenerationValidationFailureClass.AccessStrategyViolation;
            confidence = GenerationValidationConfidence.High;
        }

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

    private static RuleDecisionRecord? EvaluateAccessStrategy(
        GeneratedTestExecutionResult execution,
        CandidateMethodContext? context)
    {
        if (context?.Testability == null) return null;

        var accessPath = context.Testability.AccessPaths.FirstOrDefault();
        if (accessPath == null) return null;

        var directlyCallsTarget = DirectlyCallsTargetMethod(
            execution.GeneratedTestCode,
            context.Method.MethodName);
        var directStrategy = accessPath.Strategy is
            TestAccessStrategy.DirectPublicCall or
            TestAccessStrategy.DirectInternalCall or
            TestAccessStrategy.ProtectedSubclassHarness;
        var directTargetCallAllowed = directStrategy && accessPath.IsLegalFromTest;
        var nonPublicTarget = context.Testability.Visibility is
            MemberVisibility.Private or
            MemberVisibility.Protected or
            MemberVisibility.ExplicitInterface;
        var violated = nonPublicTarget && directlyCallsTarget && !directTargetCallAllowed;

        return RuleDecisionFactory.CreateDecision(
            "GenerationValidation",
            violated ? "AccessStrategyViolated" : "AccessStrategyRespected",
            violated
                ? GenerationValidationRuleDefinitions.AccessStrategyViolated
                : GenerationValidationRuleDefinitions.AccessStrategyRespected,
            RuleConfidence.High,
            [
                RuleDecisionFactory.CreateEvidence(
                    "ContextGraph",
                    "Visibility",
                    context.Testability.Visibility.ToString()),
                RuleDecisionFactory.CreateEvidence(
                    "ContextGraph",
                    "AccessStrategy",
                    accessPath.Strategy.ToString()),
                RuleDecisionFactory.CreateEvidence(
                    "ContextGraph",
                    "PathMemberIds",
                    string.Join(">", accessPath.PathMemberIds)),
                RuleDecisionFactory.CreateEvidence(
                    "GeneratedTest",
                    "DirectlyCallsTargetMethod",
                    directlyCallsTarget.ToString()),
                RuleDecisionFactory.CreateEvidence(
                    "GeneratedTest",
                    "TargetMethodName",
                    context.Method.MethodName)
            ],
            violated
                ? "Generated test directly called a non-public target instead of the selected context graph access path."
                : "Generated test did not violate the selected context graph access path.");
    }

    private static bool DirectlyCallsTargetMethod(string generatedTestCode, string methodName)
    {
        if (string.IsNullOrWhiteSpace(generatedTestCode) ||
            string.IsNullOrWhiteSpace(methodName))
            return false;

        var root = CSharpSyntaxTree.ParseText(generatedTestCode).GetCompilationUnitRoot();
        return root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => GetInvokedMethodName(invocation) == methodName);
    }

    private static string? GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.Text,
            _ => null
        };
    }
}
