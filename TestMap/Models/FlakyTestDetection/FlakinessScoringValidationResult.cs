namespace TestMap.Models.FlakyTestDetection;

public sealed record FlakinessScoringValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static FlakinessScoringValidationResult Success() => new(true, []);
    public static FlakinessScoringValidationResult Failure(params string[] errors) => new(false, errors);
}
