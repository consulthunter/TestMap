using TestMap.Models.Rules;

namespace TestMap.Rules.Generation;

public static class GenerationAcceptanceRuleDefinitions
{
    private const string Version = "1.0";
    private const string Category = "GenerationAcceptance";

    public static RuleDefinition CompilationSatisfied { get; } = Define("generation.acceptance.compilation-satisfied", "Compilation requirement satisfied", "Compilation was required and succeeded.");
    public static RuleDefinition CompilationFailed { get; } = Define("generation.acceptance.compilation-failed", "Compilation requirement failed", "Compilation was required and failed.");
    public static RuleDefinition TestsRan { get; } = Define("generation.acceptance.tests-ran", "Tests ran", "Tests were required to run and did run.");
    public static RuleDefinition TestsDidNotRun { get; } = Define("generation.acceptance.tests-did-not-run", "Tests did not run", "Tests were required to run but did not run.");
    public static RuleDefinition AllTestsPassed { get; } = Define("generation.acceptance.all-tests-passed", "All tests passed", "All tests were required to pass and did pass.");
    public static RuleDefinition TestFailures { get; } = Define("generation.acceptance.test-failures", "Test failures", "All tests were required to pass but failures occurred.");
    public static RuleDefinition CoverageSatisfied { get; } = Define("generation.acceptance.coverage-satisfied", "Coverage improvement satisfied", "Coverage improvement was required and present.");
    public static RuleDefinition CoverageMissing { get; } = Define("generation.acceptance.coverage-missing", "Coverage improvement missing", "Coverage improvement was required but missing.");
    public static RuleDefinition MinCoverageSatisfied { get; } = Define("generation.acceptance.min-coverage-satisfied", "Minimum coverage delta satisfied", "Minimum coverage improvement was satisfied.");
    public static RuleDefinition MinCoverageMissing { get; } = Define("generation.acceptance.min-coverage-missing", "Minimum coverage delta missing", "Minimum coverage improvement was not satisfied.");

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        CompilationSatisfied,
        CompilationFailed,
        TestsRan,
        TestsDidNotRun,
        AllTestsPassed,
        TestFailures,
        CoverageSatisfied,
        CoverageMissing,
        MinCoverageSatisfied,
        MinCoverageMissing
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
