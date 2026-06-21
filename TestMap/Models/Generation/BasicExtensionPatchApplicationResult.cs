using TestMap.Models.Rules;

namespace TestMap.Models.Generation;

/// <summary>
/// Result of applying a <see cref="BasicExtensionPatch"/> to a test file.
/// Carries outcome details, counts, and the original file contents for rollback.
/// </summary>
public sealed class BasicExtensionPatchApplicationResult
{
    /// <summary>Whether the patch was applied successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Human-readable error message when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Machine-readable outcome token.
    /// One of: <c>Success</c>, <c>TargetClassMissing</c>, <c>MalformedHelper</c>,
    /// <c>DuplicateHelper</c>, <c>MalformedTestMethod</c>, <c>DuplicateTestMethod</c>,
    /// <c>TestFileMissing</c>.
    /// </summary>
    public string PatchApplicationOutcome { get; init; } = string.Empty;

    /// <summary>Number of new <c>using</c> directives added to the file.</summary>
    public int AppliedUsingCount { get; init; }

    /// <summary>Number of helper methods appended to the target class.</summary>
    public int AppliedHelperCount { get; init; }

    /// <summary>Name of the test method appended to the target class, or null when the patch failed.</summary>
    public string? AppliedTestMethodName { get; init; }

    /// <summary>
    /// The full text of the test file before the patch was applied.
    /// Non-null on both success and failure (when the file was read).
    /// Used by the repair loop to restore the pre-patch state before applying a corrected patch.
    /// </summary>
    public string? OriginalFileContents { get; init; }

    /// <summary>Rule decisions recorded during patch application, in order.</summary>
    public IReadOnlyList<RuleDecisionRecord> RuleDecisions { get; init; } = [];
}
