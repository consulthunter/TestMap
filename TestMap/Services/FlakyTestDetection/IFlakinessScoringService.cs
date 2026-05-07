using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public interface IFlakinessScoringService
{
    FlakinessScoringValidationResult Validate(FlakyTestDetectionConfig config);

    Task<FlakyTestScoreModel> ScoreAsync(
        string runId,
        IReadOnlyList<TestExecutionResultModel> testHistory,
        FlakyTestDetectionConfig config,
        CancellationToken cancellationToken = default);
}