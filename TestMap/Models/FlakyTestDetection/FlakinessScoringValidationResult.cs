namespace TestMap.Models.FlakyTestDetection;

public sealed record FlakinessScoringValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static FlakinessScoringValidationResult Success()
    {
        return new FlakinessScoringValidationResult(true, []);
    }

    public static FlakinessScoringValidationResult Failure(params string[] errors)
    {
        return new FlakinessScoringValidationResult(false, errors);
    }
}