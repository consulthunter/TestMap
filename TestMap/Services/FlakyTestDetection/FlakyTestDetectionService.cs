using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public class FlakyTestDetectionService : IFlakyTestDetectionService
{
    private readonly IFlakinessScoringService _scoringService;
    private readonly ITestExecutionHistoryService _historyService;
    private readonly IFlakyTestRerunService _rerunService;

    public FlakyTestDetectionService(
        IFlakinessScoringService scoringService,
        ITestExecutionHistoryService historyService,
        IFlakyTestRerunService rerunService)
    {
        _scoringService = scoringService;
        _historyService = historyService;
        _rerunService = rerunService;
    }

    public async Task<FlakyTestDetectionResult> DetectAsync(
        FlakyTestDetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Config.Enabled) return new FlakyTestDetectionResult([], []);

        var validation = _scoringService.Validate(request.Config);
        if (!validation.IsValid)
            throw new InvalidOperationException(
                $"Invalid flaky test detection configuration: {string.Join("; ", validation.Errors)}");

        var failedTests = request.CurrentResults
            .Where(x => !string.Equals(x.Outcome, "Passed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var rerunResults = request.Config.RerunFailedTests
            ? await _rerunService.RerunFailedTestsAsync(request.RunId, failedTests, request.Config, cancellationToken)
            : [];

        var scores = new List<FlakyTestScoreModel>();
        foreach (var currentResult in request.CurrentResults)
        {
            var history = await _historyService.GetHistoryAsync(
                currentResult,
                request.Config.HistoryWindowRuns,
                cancellationToken);

            var combinedHistory = history.Concat([currentResult]).ToList();
            scores.Add(await _scoringService.ScoreAsync(
                request.RunId,
                combinedHistory,
                request.Config,
                cancellationToken));
        }

        return new FlakyTestDetectionResult(scores, rerunResults);
    }
}