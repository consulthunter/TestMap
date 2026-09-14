namespace TestMap.Models.Code;

public static class MemberOrigin
{
    public const string Baseline = "Baseline";
    public const string LlmAttempt = "LlmAttempt";
    public const string ToolAttempt = "ToolAttempt";
}

public sealed record MemberAnalysisOrigin(string Kind, int? AttemptId = null)
{
    public static MemberAnalysisOrigin Baseline() => new(MemberOrigin.Baseline);
    public static MemberAnalysisOrigin LlmAttempt(int? attemptId) => new(MemberOrigin.LlmAttempt, attemptId);
    public static MemberAnalysisOrigin ToolAttempt(int attemptId) => new(MemberOrigin.ToolAttempt, attemptId);
}
