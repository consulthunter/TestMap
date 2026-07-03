using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Execution;

public interface ITestActionExecutor
{
    TestActionExecutorMode Mode { get; }

    Task<TestActionExecutionResult> ExecuteAsync(
        CandidateMethodContext context,
        string generatedTest,
        string? generatedTestMethodName,
        CancellationToken cancellationToken = default);
}

public sealed class TestActionExecutionResult
{
    public bool Success { get; init; }
    public string? AppliedFilePath { get; init; }
    public string? AppliedTestMethodName { get; init; }
    public CandidateActionKind ActionKind { get; init; }
    public string? ErrorMessage { get; init; }
    /// <summary>
    /// Machine-readable outcome token set when a <c>BasicExtensionPatch</c> was applied
    /// (or a precondition was violated).  Null for legacy method-only append results.
    /// Mirrors <c>BasicExtensionPatchApplicationResult.PatchApplicationOutcome</c> and
    /// is persisted to <c>GenerationAttemptEntity</c>.
    /// </summary>
    public string? PatchApplicationOutcome { get; init; }
    /// <summary>Number of new <c>using</c> directives added by the patch applier (0 for non-patch paths).</summary>
    public int AppliedUsingCount { get; init; }
    /// <summary>Number of helper methods added by the patch applier (0 for non-patch paths).</summary>
    public int AppliedHelperCount { get; init; }
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
