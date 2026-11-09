namespace TestMap.Models.Experiment;

/// <summary>
/// Shared token-accounting classifications and invariants used by generation and agent-tool lanes.
/// </summary>
public static class TokenUsageVocabulary
{
    public const string CompleteReported = "complete-reported";
    public const string CompleteEstimated = "complete-estimated";
    public const string Partial = "partial";
    public const string Missing = "missing";
    public const string NotApplicable = "not-applicable";

    public const string PolicyV1 = "token-accounting-v1";
    public const string Cl100kLocalEstimate = "cl100k-local-estimate";

    public static bool IsComplete(string? status) =>
        string.Equals(status, CompleteReported, StringComparison.Ordinal) ||
        string.Equals(status, CompleteEstimated, StringComparison.Ordinal);

    public static int? DeriveTotal(int? inputTokens, int? outputTokens) =>
        inputTokens.HasValue && outputTokens.HasValue
            ? checked(inputTokens.Value + outputTokens.Value)
            : null;

    public static string Classify(
        int? inputTokens,
        int? outputTokens,
        bool applicable,
        bool estimated)
    {
        if (!applicable) return NotApplicable;
        if (inputTokens.HasValue && outputTokens.HasValue)
            return estimated ? CompleteEstimated : CompleteReported;
        return inputTokens.HasValue || outputTokens.HasValue ? Partial : Missing;
    }

    public static bool IsValid(string? status, int? inputTokens, int? outputTokens, int? totalTokens)
    {
        if (inputTokens < 0 || outputTokens < 0 || totalTokens < 0) return false;

        return status switch
        {
            CompleteReported or CompleteEstimated =>
                inputTokens.HasValue && outputTokens.HasValue &&
                totalTokens == DeriveTotal(inputTokens, outputTokens),
            Partial => (inputTokens.HasValue || outputTokens.HasValue) && !totalTokens.HasValue,
            Missing or NotApplicable =>
                !inputTokens.HasValue && !outputTokens.HasValue && !totalTokens.HasValue,
            _ => false
        };
    }
}
