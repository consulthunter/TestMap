namespace TestMap.Models.FlakyTestDetection;

public sealed record FlakyTestDetectionResult(
    IReadOnlyList<FlakyTestScoreModel> Scores,
    IReadOnlyList<FlakyTestRerunResultModel> RerunResults);
