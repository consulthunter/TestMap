namespace TestMap.Models.RiskScoring;

public sealed record RiskScoringValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static RiskScoringValidationResult Success()
    {
        return new RiskScoringValidationResult(true, []);
    }

    public static RiskScoringValidationResult Failure(params string[] errors)
    {
        return new RiskScoringValidationResult(false, errors);
    }
}