namespace TestMap.Models.RiskScoring;

public sealed record RiskScoringValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static RiskScoringValidationResult Success() => new(true, []);
    public static RiskScoringValidationResult Failure(params string[] errors) => new(false, errors);
}
