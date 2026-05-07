using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;

namespace TestMap.Services.TestGeneration.TargetSelection.Strategies;

public sealed class MetricDrivenCandidateSelectionStrategy : ICandidateSelectionStrategy
{
    private readonly TestMapDbContext _dbContext;

    public MetricDrivenCandidateSelectionStrategy(TestMapDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public TargetSelectionStrategy Strategy => TargetSelectionStrategy.MetricDrivenImprovement;

    public async Task<IReadOnlyList<CandidateMethod>> SelectAsync(
        CandidateSelectionContext context,
        IReadOnlyList<CandidateSelectionRow> candidatePool,
        CancellationToken cancellationToken = default)
    {
        var scoresByMemberId = await ScoreCandidatesAsync(
            candidatePool,
            context.TargetSelection.MetricDrivenImprovement,
            cancellationToken);
        var targetLimit = Math.Min(
            context.EffectiveLimit,
            Math.Max(1, context.TargetSelection.MetricDrivenImprovement.Budget.MaxTargets));

        return candidatePool
            .OrderByDescending(x => scoresByMemberId.TryGetValue(x.Id, out var score) ? score.Score : 0.0)
            .ThenByDescending(x => scoresByMemberId.TryGetValue(x.Id, out var score) ? score.ExpectedMetricDelta : 0.0)
            .ThenBy(x => x.LineRate)
            .Take(targetLimit)
            .Select(row => CandidateMethodFactory.Create(
                row,
                context.SelectionTime,
                metricScore: scoresByMemberId.TryGetValue(row.Id, out var score) ? score : null))
            .ToList();
    }

    private async Task<Dictionary<int, MetricDrivenCandidateScore>> ScoreCandidatesAsync(
        IReadOnlyList<CandidateSelectionRow> candidates,
        MetricDrivenImprovementConfig config,
        CancellationToken cancellationToken)
    {
        ValidateWeights(config);

        if (candidates.Count == 0) return new Dictionary<int, MetricDrivenCandidateScore>();

        var memberIds = candidates.Select(x => x.Id).ToList();
        var latestCoverageByMemberId = (await _dbContext.MemberCoverages
                .Where(x => memberIds.Contains(x.MemberId))
                .OrderByDescending(x => x.CoverageReportId)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.MemberId)
            .ToDictionary(x => x.Key, x => x.First());

        var mutantsByMemberId = (await _dbContext.Mutants
                .Where(x => x.MemberId.HasValue && memberIds.Contains(x.MemberId.Value))
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.MemberId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var coverageGapsByMemberId = await _dbContext.CoverageGaps
            .Where(x => memberIds.Contains(x.MemberId))
            .GroupBy(x => x.MemberId)
            .Select(x => new { MemberId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.MemberId, x => x.Count, cancellationToken);

        var directTestSignalsByMemberId = await GetDirectTestSignalsAsync(memberIds, cancellationToken);
        var totalMutants = await _dbContext.Mutants.CountAsync(x => x.MemberId != null, cancellationToken);
        var totalSurvivingMutants = await _dbContext.Mutants.CountAsync(
            x => x.MemberId != null &&
                 (x.Status == "Survived" ||
                  x.Status == "survived" ||
                  x.Status == "NoCoverage" ||
                  x.Status == "noCoverage" ||
                  x.Status == "nocoverage"),
            cancellationToken);
        var totalCoverageGaps = await _dbContext.CoverageGaps.CountAsync(cancellationToken);
        var latestCoverageRows = latestCoverageByMemberId.Values.ToList();
        var poolLinesValid = Math.Max(1, latestCoverageRows.Sum(x => x.LinesValid));
        var poolBranchesValid = Math.Max(1, latestCoverageRows.Sum(x => x.BranchesValid));
        var noTestSignalCount = Math.Max(
            1,
            candidates.Count(x =>
                !directTestSignalsByMemberId.TryGetValue(x.Id, out var signalCount) || signalCount == 0));
        var uncoveredPublicMethodCount = Math.Max(
            1,
            candidates.Count(x => IsPublicMethod(x.FullString) &&
                                  latestCoverageByMemberId.TryGetValue(x.Id, out var coverage) &&
                                  coverage.LineRate <= 0.0));
        var weights = config.Weights.ToDictionary();
        var results = new Dictionary<int, MetricDrivenCandidateScore>();

        foreach (var candidate in candidates)
        {
            latestCoverageByMemberId.TryGetValue(candidate.Id, out var coverage);
            mutantsByMemberId.TryGetValue(candidate.Id, out var mutants);
            coverageGapsByMemberId.TryGetValue(candidate.Id, out var coverageGapCount);
            directTestSignalsByMemberId.TryGetValue(candidate.Id, out var directTestSignals);

            var expectedDelta = CalculateExpectedMetricDelta(
                candidate,
                config.Metric,
                coverage,
                mutants ?? [],
                coverageGapCount,
                directTestSignals,
                totalMutants,
                totalSurvivingMutants,
                totalCoverageGaps,
                poolLinesValid,
                poolBranchesValid,
                uncoveredPublicMethodCount,
                noTestSignalCount);
            var confidence = CalculateMetricConfidence(config.Metric, coverage, mutants?.Count ?? 0, coverageGapCount);
            var feasibility = CalculateMetricFeasibility(candidate, coverage, directTestSignals);
            var estimatedCost = CalculateEstimatedCost(candidate, coverage);
            var inverseCost = 1.0 - estimatedCost;
            var guardrailStatus = ResolveGuardrailStatus(config, candidate, expectedDelta);
            var guardrailScore = guardrailStatus == "failed" ? 0.0 : guardrailStatus == "warning" ? 0.5 : 1.0;
            var score = (expectedDelta * weights[nameof(MetricDrivenWeightsConfig.ExpectedMetricDelta)] +
                         confidence * weights[nameof(MetricDrivenWeightsConfig.Confidence)] +
                         feasibility * weights[nameof(MetricDrivenWeightsConfig.Feasibility)] +
                         inverseCost * weights[nameof(MetricDrivenWeightsConfig.InverseCost)] +
                         guardrailScore * weights[nameof(MetricDrivenWeightsConfig.Guardrail)]) * 100.0;

            if (config.Guardrails.ExcludeFailedGuardrails && guardrailStatus == "failed") score = 0.0;

            results[candidate.Id] = new MetricDrivenCandidateScore(
                candidate.Id,
                score,
                expectedDelta,
                confidence,
                feasibility,
                estimatedCost,
                guardrailStatus,
                BuildEvidence(
                    config.Metric,
                    coverage,
                    mutants?.Count ?? 0,
                    mutants?.Count(x => IsUndetectedMutantStatus(x.Status)) ?? 0,
                    coverageGapCount,
                    directTestSignals));
        }

        return results;
    }

    private async Task<Dictionary<int, int>> GetDirectTestSignalsAsync(
        IReadOnlyCollection<int> memberIds,
        CancellationToken cancellationToken)
    {
        var relationshipSignals = await _dbContext.MemberRelationships
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.TargetId))
            .Where(x => x.RelationshipType == "tests" || x.RelationshipType == "covers")
            .GroupBy(x => x.TargetId)
            .Select(x => new { MemberId = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);
        var invocationSignals = await (
                from invocation in _dbContext.Invocations
                join member in _dbContext.Members on invocation.MemberId equals member.Id
                where invocation.InvokedMemberId.HasValue &&
                      memberIds.Contains(invocation.InvokedMemberId.Value) &&
                      member.IsTestMember
                group invocation by invocation.InvokedMemberId!.Value
                into grouped
                select new { MemberId = grouped.Key, Count = grouped.Count() })
            .ToListAsync(cancellationToken);

        return relationshipSignals
            .Concat(invocationSignals)
            .GroupBy(x => x.MemberId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Count));
    }

    private static void ValidateWeights(MetricDrivenImprovementConfig config)
    {
        const double minimumWeight = 0.01;
        const double maximumWeight = 0.99;
        const double tolerance = 0.0001;
        var weights = config.Weights.ToDictionary();
        var invalid = weights
            .Where(x => x.Value is < minimumWeight or > maximumWeight)
            .Select(x => $"{x.Key}={x.Value:0.####}")
            .ToList();

        if (invalid.Count > 0)
            throw new InvalidOperationException(
                $"Metric-driven weights must be between {minimumWeight:0.00} and {maximumWeight:0.00}: {string.Join(", ", invalid)}.");

        var total = weights.Values.Sum();
        if (Math.Abs(total - 1.0) > tolerance)
            throw new InvalidOperationException(
                $"Metric-driven weights must total 1.0. Current total is {total:0.####}.");
    }

    private static double CalculateExpectedMetricDelta(
        CandidateSelectionRow candidate,
        MetricDrivenMetric metric,
        Persistence.Ef.Entities.Coverage.MemberCoverageEntity? coverage,
        IReadOnlyCollection<Persistence.Ef.Entities.MutationTesting.MutantEntity> mutants,
        int coverageGapCount,
        int directTestSignals,
        int totalMutants,
        int totalSurvivingMutants,
        int totalCoverageGaps,
        int poolLinesValid,
        int poolBranchesValid,
        int uncoveredPublicMethodCount,
        int noTestSignalCount)
    {
        var undetectedMutants = mutants.Count(x => IsUndetectedMutantStatus(x.Status));
        return Clamp01(metric switch
        {
            MetricDrivenMetric.MutationScore => totalMutants > 0 ? (double)undetectedMutants / totalMutants : 0.0,
            MetricDrivenMetric.SurvivingMutants => totalSurvivingMutants > 0
                ? (double)undetectedMutants / totalSurvivingMutants
                : 0.0,
            MetricDrivenMetric.LineCoverage => coverage == null
                ? 0.0
                : (double)Math.Max(0, coverage.LinesValid - coverage.LinesCovered) / poolLinesValid,
            MetricDrivenMetric.BranchCoverage => coverage == null
                ? 0.0
                : (double)Math.Max(0, coverage.BranchesValid - coverage.BranchesCovered) / poolBranchesValid,
            MetricDrivenMetric.CoverageGaps => totalCoverageGaps > 0
                ? (double)coverageGapCount / totalCoverageGaps
                : 0.0,
            MetricDrivenMetric.UncoveredPublicMethods => IsPublicMethod(candidate.FullString) &&
                                                         coverage?.LineRate <= 0.0
                ? 1.0 / uncoveredPublicMethodCount
                : 0.0,
            MetricDrivenMetric.SourceMethodsWithNoTests => directTestSignals == 0 ? 1.0 / noTestSignalCount : 0.0,
            MetricDrivenMetric.TestSmellCount => 0.0,
            MetricDrivenMetric.FlakyTestCount => 0.0,
            MetricDrivenMetric.AssertionQuality => directTestSignals > 0 && undetectedMutants > 0
                ? Math.Min(1.0, undetectedMutants / 5.0)
                : 0.0,
            _ => 0.0
        });
    }

    private static double CalculateMetricConfidence(
        MetricDrivenMetric metric,
        Persistence.Ef.Entities.Coverage.MemberCoverageEntity? coverage,
        int mutantCount,
        int coverageGapCount)
    {
        var score = 0.35;
        if (coverage != null) score += 0.25;

        if (metric is MetricDrivenMetric.MutationScore or MetricDrivenMetric.SurvivingMutants
                or MetricDrivenMetric.AssertionQuality &&
            mutantCount > 0)
            score += 0.25;

        if (metric is MetricDrivenMetric.LineCoverage or MetricDrivenMetric.BranchCoverage
                or MetricDrivenMetric.CoverageGaps &&
            coverageGapCount > 0)
            score += 0.25;

        return Clamp01(score);
    }

    private static double CalculateMetricFeasibility(
        CandidateSelectionRow candidate,
        Persistence.Ef.Entities.Coverage.MemberCoverageEntity? coverage,
        int directTestSignals)
    {
        var complexityPenalty = Math.Min(0.5, Math.Max(0, candidate.Complexity) / 40.0);
        var coverageSignalBonus = coverage != null ? 0.2 : 0.0;
        var testSignalBonus = directTestSignals > 0 ? 0.2 : 0.0;

        return Clamp01(0.65 + coverageSignalBonus + testSignalBonus - complexityPenalty);
    }

    private static double CalculateEstimatedCost(
        CandidateSelectionRow candidate,
        Persistence.Ef.Entities.Coverage.MemberCoverageEntity? coverage)
    {
        var complexityCost = Math.Min(0.65, Math.Max(0, candidate.Complexity) / 50.0);
        var sourceLengthCost = Math.Min(0.25, candidate.FullString.Length / 8000.0);
        var noCoveragePenalty = coverage == null ? 0.1 : 0.0;

        return Clamp01(complexityCost + sourceLengthCost + noCoveragePenalty);
    }

    private static string ResolveGuardrailStatus(
        MetricDrivenImprovementConfig config,
        CandidateSelectionRow candidate,
        double expectedDelta)
    {
        if (expectedDelta < 0.01) return "failed";

        if (config.Guardrails.RequireMeaningfulAssertions &&
            candidate.FullString.Length > Math.Max(4000, config.Budget.MaxGenerationAttempts * 200))
            return "warning";

        if (config.Guardrails.AvoidImplementationDetailAssertions && candidate.Complexity > 40) return "warning";

        return "passed";
    }

    private static string BuildEvidence(
        MetricDrivenMetric metric,
        Persistence.Ef.Entities.Coverage.MemberCoverageEntity? coverage,
        int mutantCount,
        int undetectedMutants,
        int coverageGapCount,
        int directTests)
    {
        return $"metric={metric}; coverage={(coverage == null ? "n/a" : coverage.LineRate.ToString("P1"))}; " +
               $"mutants={mutantCount}; undetected_mutants={undetectedMutants}; coverage_gaps={coverageGapCount}; " +
               $"direct_test_signals={directTests}";
    }

    private static bool IsUndetectedMutantStatus(string status)
    {
        return status.Equals("Survived", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("NoCoverage", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicMethod(string fullString)
    {
        return fullString.Contains("public ", StringComparison.Ordinal) ||
               fullString.Contains("protected ", StringComparison.Ordinal);
    }

    private static double Clamp01(double value)
    {
        return Math.Min(1.0, Math.Max(0.0, value));
    }
}
