using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Services.FlakyTestDetection;

public class FlakinessScoringService : IFlakinessScoringService
{
    private const double WeightTolerance = 0.0001;
    private readonly IReadOnlyDictionary<FlakinessFactorKind, IFlakinessFactorProvider> _providers;

    public FlakinessScoringService(IEnumerable<IFlakinessFactorProvider> providers)
    {
        _providers = providers.ToDictionary(x => x.Factor);
    }

    public FlakinessScoringValidationResult Validate(FlakyTestDetectionConfig config)
    {
        var errors = new List<string>();
        var weights = config.Weights.ToDictionary();

        foreach (var (factor, weight) in weights)
        {
            if (weight is < 0.01 or > 0.99)
            {
                errors.Add($"{factor} weight must be between 0.01 and 0.99.");
            }

            if (!_providers.ContainsKey(factor))
            {
                errors.Add($"No flaky test factor provider is registered for {factor}.");
            }
        }

        var total = weights.Values.Sum();
        if (Math.Abs(total - 1.0) > WeightTolerance)
        {
            errors.Add($"Flakiness weights must sum to 1.0. Actual sum: {total:0.####}.");
        }

        if (config.MinimumExecutions < 2)
        {
            errors.Add("MinimumExecutions must be at least 2.");
        }

        if (config.HistoryWindowRuns < config.MinimumExecutions)
        {
            errors.Add("HistoryWindowRuns must be greater than or equal to MinimumExecutions.");
        }

        if (config.RerunFailedTests && config.RerunCount < 1)
        {
            errors.Add("RerunCount must be at least 1 when RerunFailedTests is enabled.");
        }

        return errors.Count == 0
            ? FlakinessScoringValidationResult.Success()
            : new FlakinessScoringValidationResult(false, errors);
    }

    public async Task<FlakyTestScoreModel> ScoreAsync(
        string runId,
        IReadOnlyList<TestExecutionResultModel> testHistory,
        FlakyTestDetectionConfig config,
        CancellationToken cancellationToken = default)
    {
        var latest = testHistory.LastOrDefault();
        if (latest == null)
        {
            return new FlakyTestScoreModel
            {
                RunId = runId,
                Classification = FlakyTestClassification.InsufficientData,
                Evidence = ["No test execution history was provided."]
            };
        }

        if (testHistory.Count < config.MinimumExecutions)
        {
            return new FlakyTestScoreModel
            {
                RunId = runId,
                TestMemberId = latest.TestMemberId,
                TestName = latest.TestName,
                FilePath = latest.FilePath,
                Classification = FlakyTestClassification.InsufficientData,
                Evidence = [$"Only {testHistory.Count} execution(s) available; {config.MinimumExecutions} required."]
            };
        }

        var weights = config.Weights.ToDictionary();
        var factorScores = new Dictionary<FlakinessFactorKind, double>();
        var evidence = new List<string>();
        var weightedScore = 0.0;

        foreach (var (factor, weight) in weights)
        {
            var score = await _providers[factor].GetScoreAsync(testHistory, cancellationToken);
            var normalizedScore = Math.Clamp(score.Score, 0.0, 1.0);
            factorScores[factor] = normalizedScore;
            weightedScore += normalizedScore * weight;

            if (!string.IsNullOrWhiteSpace(score.Evidence))
            {
                evidence.Add(score.Evidence);
            }
        }

        var finalScore = weightedScore * 100;
        return new FlakyTestScoreModel
        {
            RunId = runId,
            TestMemberId = latest.TestMemberId,
            TestName = latest.TestName,
            FilePath = latest.FilePath,
            FlakinessScore = finalScore,
            Classification = Classify(finalScore, config.FlakyScoreThreshold),
            FactorScores = factorScores,
            Weights = weights.ToDictionary(x => x.Key, x => x.Value),
            Evidence = evidence
        };
    }

    private static FlakyTestClassification Classify(double score, double threshold)
    {
        if (score >= threshold)
        {
            return FlakyTestClassification.LikelyFlaky;
        }

        if (score >= threshold / 2)
        {
            return FlakyTestClassification.PotentiallyFlaky;
        }

        return FlakyTestClassification.Stable;
    }
}
