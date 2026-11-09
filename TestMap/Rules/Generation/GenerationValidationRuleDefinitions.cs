using TestMap.Models.Rules;

namespace TestMap.Rules.Generation;

public static class GenerationValidationRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "GenerationValidation";

    public static RuleDefinition CodeExtracted { get; } = Define("generation.validation.code-extracted", "Generated code extracted", "Generated test code was present.");
    public static RuleDefinition CodeMissing { get; } = Define("generation.validation.code-missing", "Generated code missing", "Generated test code was missing.");
    public static RuleDefinition MethodNameExtracted { get; } = Define("generation.validation.method-name-extracted", "Test method name extracted", "A generated test method name was present.");
    public static RuleDefinition MethodNameMissing { get; } = Define("generation.validation.method-name-missing", "Test method name missing", "No generated test method name was present.");
    public static RuleDefinition SyntaxValid { get; } = Define("generation.validation.syntax-valid", "Syntax valid", "Generated test syntax was considered valid.");
    public static RuleDefinition SyntaxInvalid { get; } = Define("generation.validation.syntax-invalid", "Syntax invalid", "Generated test syntax was invalid.");
    public static RuleDefinition CompilationSucceeded { get; } = Define("generation.validation.compilation-succeeded", "Compilation succeeded", "The project compiled after applying the generated test.");
    public static RuleDefinition CompilationFailed { get; } = Define("generation.validation.compilation-failed", "Compilation failed", "The project did not compile after applying the generated test.");
    public static RuleDefinition TestsExecuted { get; } = Define("generation.validation.tests-executed", "Tests executed", "The test run produced executed test results.");
    public static RuleDefinition TestsNotExecuted { get; } = Define("generation.validation.tests-not-executed", "Tests did not execute", "No tests executed after applying the generated test.");
    public static RuleDefinition AllTestsPassed { get; } = Define("generation.validation.all-tests-passed", "All tests passed", "The generated test run completed with all tests passing.");
    public static RuleDefinition TestFailure { get; } = Define("generation.validation.test-failure", "Test failure", "At least one test failed, threw, or asserted incorrectly.");
    public static RuleDefinition CoverageImproved { get; } = Define("generation.validation.coverage-improved", "Coverage improved", "Coverage improved after applying the generated test.");
    public static RuleDefinition CoverageDidNotImprove { get; } = Define("generation.validation.coverage-did-not-improve", "Coverage did not improve", "Coverage did not improve after applying the generated test.");
    public static RuleDefinition MutationImproved { get; } = Define("generation.validation.mutation-improved", "Mutation score improved", "Mutation score improved after applying the generated test.");
    public static RuleDefinition MutationUnavailable { get; } = Define("generation.validation.mutation-unavailable", "Mutation result unavailable", "Mutation validation was not available or not applicable.");
    public static RuleDefinition UsefulMetricSignal { get; } = Define("generation.validation.useful-metric-signal", "Useful metric signal", "The generated test produced useful coverage or mutation signal.");
    public static RuleDefinition OutcomeSelected { get; } = Define("generation.validation.outcome-selected", "Validation outcome selected", "The validation outcome was selected from observed execution and diagnostic evidence.");
    public static RuleDefinition ConfidenceSelected { get; } = Define("generation.validation.confidence-selected", "Validation confidence selected", "The validation confidence was selected from the strength of execution and diagnostic evidence.");
    public static RuleDefinition FailureClassSelected { get; } = Define("generation.validation.failure-class-selected", "Validation failure class selected", "The validation failure class was selected from execution and diagnostic evidence.");
    public static RuleDefinition DiagnosticComparison { get; } = Define("generation.validation.diagnostic-comparison", "Roslyn diagnostics compared", "Before and after Roslyn diagnostics were compared for the modified test file.");
    public static RuleDefinition DiagnosticInfrastructureNoise { get; } = Define("generation.validation.diagnostic-infrastructure-noise", "Roslyn infrastructure diagnostics", "Roslyn diagnostics were classified as infrastructure or design-time noise.");
    public static RuleDefinition DiagnosticGeneratedCodeAttributed { get; } = Define("generation.validation.diagnostic-generated-code-attributed", "Generated-code diagnostic attribution", "New diagnostics were attributed to generated code or insertion when evidence was local and high confidence.");
    public static RuleDefinition PreBuildAllowed { get; } = Define("generation.validation.pre-build-allowed", "Pre-build gate allowed build", "The Roslyn pre-build gate allowed real build/test validation.");
    public static RuleDefinition PreBuildSkipped { get; } = Define("generation.validation.pre-build-skipped", "Pre-build gate skipped build", "The Roslyn pre-build gate skipped real build/test validation due to high-confidence local generated-test defects.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        CodeExtracted,
        CodeMissing,
        MethodNameExtracted,
        MethodNameMissing,
        SyntaxValid,
        SyntaxInvalid,
        CompilationSucceeded,
        CompilationFailed,
        TestsExecuted,
        TestsNotExecuted,
        AllTestsPassed,
        TestFailure,
        CoverageImproved,
        CoverageDidNotImprove,
        MutationImproved,
        MutationUnavailable,
        UsefulMetricSignal,
        OutcomeSelected,
        ConfidenceSelected,
        FailureClassSelected,
        DiagnosticComparison,
        DiagnosticInfrastructureNoise,
        DiagnosticGeneratedCodeAttributed,
        PreBuildAllowed,
        PreBuildSkipped
    ];

    private static RuleDefinition Define(string id, string name, string description)
    {
        return new RuleDefinition
        {
            Id = id,
            Version = Version,
            Name = name,
            Description = description,
            Category = Category
        };
    }
}
