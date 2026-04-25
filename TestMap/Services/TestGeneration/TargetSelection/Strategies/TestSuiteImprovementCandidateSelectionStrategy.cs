using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;

namespace TestMap.Services.TestGeneration.TargetSelection.Strategies;

public sealed class TestSuiteImprovementCandidateSelectionStrategy : ICandidateSelectionStrategy
{
    private readonly TestMapDbContext _dbContext;

    public TestSuiteImprovementCandidateSelectionStrategy(TestMapDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public TargetSelectionStrategy Strategy => TargetSelectionStrategy.TestSuiteImprovements;

    public async Task<IReadOnlyList<CandidateMethod>> SelectAsync(
        CandidateSelectionContext context,
        IReadOnlyList<CandidateSelectionRow> candidatePool,
        CancellationToken cancellationToken = default)
    {
        var scoresByMemberId = await ScoreCandidatesAsync(
            candidatePool,
            context.TargetSelection.TestSuiteImprovement,
            cancellationToken);

        return candidatePool
            .Where(x => scoresByMemberId.ContainsKey(x.Id))
            .OrderByDescending(x => scoresByMemberId[x.Id].Score)
            .ThenBy(x => x.LineRate)
            .Take(context.EffectiveLimit)
            .Select(row =>
            {
                var candidate = CandidateMethodFactory.Create(row, context.SelectionTime);
                var score = scoresByMemberId[row.Id];
                candidate.TestImprovementScore = score.Score;
                candidate.TestImprovementReason = score.Evidence;
                candidate.RecommendedAction = CandidateActionKind.ImproveExistingTest;
                return candidate;
            })
            .ToList();
    }

    private async Task<Dictionary<int, TestSuiteImprovementCandidateScore>> ScoreCandidatesAsync(
        IReadOnlyList<CandidateSelectionRow> candidates,
        TestSuiteImprovementConfig config,
        CancellationToken cancellationToken)
    {
        ValidateWeights(config);

        if (candidates.Count == 0) return new Dictionary<int, TestSuiteImprovementCandidateScore>();

        var sourceMemberIds = candidates.Select(x => x.Id).ToList();
        var linkedTestMembersBySourceMemberId =
            await GetLinkedTestMembersBySourceMemberIdAsync(sourceMemberIds, cancellationToken);
        var linkedTestMemberIds = linkedTestMembersBySourceMemberId.Values
            .SelectMany(x => x)
            .Distinct()
            .ToList();

        if (linkedTestMemberIds.Count == 0) return new Dictionary<int, TestSuiteImprovementCandidateScore>();

        var linkedTestMembers = await _dbContext.Members
            .AsNoTracking()
            .Where(x => linkedTestMemberIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var smellCounts = await _dbContext.TestSmells
            .AsNoTracking()
            .Where(x => x.MemberId.HasValue && linkedTestMemberIds.Contains(x.MemberId.Value))
            .GroupBy(x => x.MemberId!.Value)
            .Select(x => new { TestMemberId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.TestMemberId, x => x.Count, cancellationToken);
        var latestFlakyScores = (await _dbContext.FlakyTestScores
                .AsNoTracking()
                .Where(x => x.TestMemberId.HasValue && linkedTestMemberIds.Contains(x.TestMemberId.Value))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.TestMemberId!.Value)
            .ToDictionary(x => x.Key, x => x.First().FlakinessScore / 100.0);
        var invocationSummaries = await _dbContext.Invocations
            .AsNoTracking()
            .Where(x => linkedTestMemberIds.Contains(x.MemberId))
            .GroupBy(x => x.MemberId)
            .Select(x => new
            {
                TestMemberId = x.Key,
                TotalInvocations = x.Count(),
                AssertionInvocations = x.Count(y => y.IsAssertion)
            })
            .ToDictionaryAsync(x => x.TestMemberId, cancellationToken);
        var mutationSummaries = await _dbContext.Mutants
            .AsNoTracking()
            .Where(x => x.MemberId.HasValue && sourceMemberIds.Contains(x.MemberId.Value))
            .GroupBy(x => x.MemberId!.Value)
            .Select(x => new
            {
                SourceMemberId = x.Key,
                Total = x.Count(),
                Undetected = x.Count(y => y.Status == "Survived" || y.Status == "NoCoverage")
            })
            .ToDictionaryAsync(x => x.SourceMemberId, cancellationToken);
        var weights = config.Weights.ToDictionary();
        var results = new Dictionary<int, TestSuiteImprovementCandidateScore>();

        foreach (var candidate in candidates)
        {
            if (!linkedTestMembersBySourceMemberId.TryGetValue(candidate.Id, out var testMemberIds) ||
                testMemberIds.Count == 0) continue;

            var bestTestMemberId = testMemberIds
                .Where(linkedTestMembers.ContainsKey)
                .OrderByDescending(id => smellCounts.GetValueOrDefault(id))
                .ThenByDescending(id => latestFlakyScores.GetValueOrDefault(id))
                .ThenByDescending(id => linkedTestMembers[id].FullString.Length)
                .FirstOrDefault();

            if (bestTestMemberId == 0 || !linkedTestMembers.TryGetValue(bestTestMemberId, out var testMember)) continue;

            var smellScore = Clamp01(smellCounts.GetValueOrDefault(bestTestMemberId) / 3.0);
            var flakinessScore = Clamp01(latestFlakyScores.GetValueOrDefault(bestTestMemberId));
            invocationSummaries.TryGetValue(bestTestMemberId, out var invocationSummary);
            var assertionQualityScore = CalculateAssertionQualityScore(
                invocationSummary?.TotalInvocations ?? 0,
                invocationSummary?.AssertionInvocations ?? 0);
            mutationSummaries.TryGetValue(candidate.Id, out var mutationSummary);
            var mutationWeaknessScore = CalculateMutationWeaknessScore(
                mutationSummary?.Total ?? 0,
                mutationSummary?.Undetected ?? 0);
            var coverageValueScore = CalculateCoverageValueScore(candidate);
            var maintenanceRiskScore = CalculateMaintenanceRiskScore(testMember.FullString.Length,
                smellCounts.GetValueOrDefault(bestTestMemberId));
            var score = (mutationWeaknessScore * weights[nameof(TestSuiteImprovementWeightsConfig.MutationWeakness)] +
                         smellScore * weights[nameof(TestSuiteImprovementWeightsConfig.TestSmells)] +
                         assertionQualityScore * weights[nameof(TestSuiteImprovementWeightsConfig.AssertionQuality)] +
                         flakinessScore * weights[nameof(TestSuiteImprovementWeightsConfig.Flakiness)] +
                         coverageValueScore * weights[nameof(TestSuiteImprovementWeightsConfig.CoverageValue)] +
                         maintenanceRiskScore * weights[nameof(TestSuiteImprovementWeightsConfig.MaintenanceRisk)]) *
                        100.0;
            var recommendedAction = RecommendAction(
                mutationWeaknessScore,
                smellScore,
                assertionQualityScore,
                flakinessScore,
                maintenanceRiskScore);
            var evidence = $"test_member_id={bestTestMemberId}; score={score:0.00}; action={recommendedAction}; " +
                           $"mutation_weakness={mutationWeaknessScore:0.00}; test_smells={smellScore:0.00}; " +
                           $"assertion_quality={assertionQualityScore:0.00}; flakiness={flakinessScore:0.00}; " +
                           $"coverage_value={coverageValueScore:0.00}; maintenance_risk={maintenanceRiskScore:0.00}";

            results[candidate.Id] = new TestSuiteImprovementCandidateScore(
                candidate.Id,
                score,
                recommendedAction,
                evidence);
        }

        return results;
    }

    private async Task<Dictionary<int, List<int>>> GetLinkedTestMembersBySourceMemberIdAsync(
        IReadOnlyCollection<int> sourceMemberIds,
        CancellationToken cancellationToken)
    {
        var relationshipLinks = await _dbContext.MemberRelationships
            .AsNoTracking()
            .Where(x => sourceMemberIds.Contains(x.TargetId))
            .Where(x => x.RelationshipType == "tests" || x.RelationshipType == "covers")
            .Select(x => new { SourceMemberId = x.TargetId, TestMemberId = x.SourceId })
            .ToListAsync(cancellationToken);
        var invocationLinks = await (
                from invocation in _dbContext.Invocations.AsNoTracking()
                join member in _dbContext.Members.AsNoTracking() on invocation.MemberId equals member.Id
                where invocation.InvokedMemberId.HasValue &&
                      sourceMemberIds.Contains(invocation.InvokedMemberId.Value) &&
                      member.IsTestMember
                select new
                {
                    SourceMemberId = invocation.InvokedMemberId!.Value,
                    TestMemberId = invocation.MemberId
                })
            .ToListAsync(cancellationToken);

        return relationshipLinks
            .Concat(invocationLinks)
            .GroupBy(x => x.SourceMemberId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.TestMemberId).Distinct().ToList());
    }

    private static void ValidateWeights(TestSuiteImprovementConfig config)
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
                $"Test-suite-improvement weights must be between {minimumWeight:0.00} and {maximumWeight:0.00}: {string.Join(", ", invalid)}.");

        var total = weights.Values.Sum();
        if (Math.Abs(total - 1.0) > tolerance)
            throw new InvalidOperationException(
                $"Test-suite-improvement weights must total 1.0. Current total is {total:0.####}.");
    }

    private static double CalculateAssertionQualityScore(int totalInvocations, int assertionInvocations)
    {
        if (totalInvocations == 0) return 0.6;

        if (assertionInvocations == 0) return 1.0;

        var strength = Math.Min(1.0, (double)assertionInvocations / totalInvocations);
        return Clamp01(1.0 - strength);
    }

    private static double CalculateMutationWeaknessScore(int totalMutants, int undetectedMutants)
    {
        if (totalMutants <= 0) return 0.0;

        return Clamp01((double)undetectedMutants / totalMutants);
    }

    private static double CalculateCoverageValueScore(CandidateSelectionRow candidate)
    {
        var complexity = Clamp01(candidate.Complexity / 30.0);
        var publicSurface = candidate.FullString.Contains("public ", StringComparison.Ordinal) ? 0.25 : 0.0;
        var coverageNeed = Clamp01(1.0 - candidate.LineRate);
        return Clamp01(complexity * 0.4 + publicSurface + coverageNeed * 0.35);
    }

    private static double CalculateMaintenanceRiskScore(int testBodyLength, int smellCount)
    {
        var bodyLengthRisk = Clamp01(testBodyLength / 2500.0);
        var smellRisk = Clamp01(smellCount / 4.0);
        return Clamp01(bodyLengthRisk * 0.6 + smellRisk * 0.4);
    }

    private static string RecommendAction(
        double mutationWeaknessScore,
        double smellScore,
        double assertionQualityScore,
        double flakinessScore,
        double maintenanceRiskScore)
    {
        var factors = new Dictionary<string, double>
        {
            ["flakiness"] = flakinessScore,
            ["mutation"] = mutationWeaknessScore,
            ["assertion"] = assertionQualityScore,
            ["smells"] = smellScore,
            ["maintenance"] = maintenanceRiskScore
        };

        return factors.OrderByDescending(x => x.Value).First().Key switch
        {
            "flakiness" => "stabilize_test",
            "mutation" => "increase_mutation_kill_strength",
            "assertion" => "strengthen_assertions",
            "maintenance" => "split_test",
            _ => "reduce_setup"
        };
    }

    private static double Clamp01(double value)
    {
        return Math.Min(1.0, Math.Max(0.0, value));
    }
}