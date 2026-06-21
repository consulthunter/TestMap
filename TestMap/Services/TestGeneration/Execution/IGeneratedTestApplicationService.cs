using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Execution;

public interface IGeneratedTestApplicationService
{
    Task<GeneratedTestApplicationResult> ApplyAsync(
        CandidateMethodContext context,
        string generatedTest,
        string testMethodName,
        TestActionExecutorMode mode,
        CancellationToken cancellationToken = default);
}

public sealed class GeneratedTestApplicationResult
{
    public bool Success { get; init; }
    public string? AppliedFilePath { get; init; }
    public string? AppliedTestMethodName { get; init; }
    public CandidateActionKind ActionKind { get; init; }
    public string? ErrorMessage { get; init; }
    /// <summary>
    /// Machine-readable patch outcome token from <c>BasicExtensionPatchApplicationService</c>.
    /// Null for legacy method-only append paths.
    /// </summary>
    public string? PatchApplicationOutcome { get; init; }
    /// <summary>Number of new <c>using</c> directives added by the patch applier (0 for non-patch paths).</summary>
    public int AppliedUsingCount { get; init; }
    /// <summary>Number of helper methods added by the patch applier (0 for non-patch paths).</summary>
    public int AppliedHelperCount { get; init; }
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
