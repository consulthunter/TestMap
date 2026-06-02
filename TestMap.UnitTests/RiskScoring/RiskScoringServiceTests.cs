using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;
using TestMap.Services.RiskScoring;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.RiskScoring;

/// <summary>
/// Unit tests for <see cref="RiskScoringService"/> covering weight validation, weighted
/// score computation, result ordering, missing-provider handling, evidence formatting,
/// and per-candidate factor score population. All tests use lightweight
/// <see cref="FixedScoreProvider"/> / <see cref="VariableScoreProvider"/> test doubles so
/// no database is required.
/// </summary>
public sealed class RiskScoringServiceTests
{
    // ── ValidateWeights ───────────────────────────────────────────────────────

    /// <summary>
    /// Default weights (sum = 1.0, each between 0.01 and 0.99) pass validation.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateWeights_ValidWeights_ReturnsSuccess()
    {
        var service = BuildService(providerScore: 0.5);
        var result = service.ValidateWeights(RequestFor([]));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// A CoverageGap weight of 0.005 is below the 0.01 minimum — validation returns an
    /// error that names the offending factor.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateWeights_WeightOutOfRange_ReturnsErrorNamingFactor()
    {
        var service = BuildService(providerScore: 0.5);
        var config = ValidTargetSelectionConfig();
        // CoverageGap 0.005 < 0.01; other weights adjusted so sum stays at 1.0
        config.RiskWeights = new RiskWeightsConfig
        {
            CoverageGap      = 0.005,
            MutationSurvival = 0.25,
            Complexity       = 0.25,
            CallGraph        = 0.25,
            Churn            = 0.12,
            TestGap          = 0.125
        };

        var result = service.ValidateWeights(RequestFor([], config));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("CoverageGap"));
    }

    /// <summary>
    /// Increasing CoverageGap from 0.30 to 0.40 makes the weights sum to 1.10 — validation
    /// returns an error that mentions "total".
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateWeights_WeightsDoNotSumToOne_ReturnsErrorMentioningTotal()
    {
        var service = BuildService(providerScore: 0.5);
        var config = ValidTargetSelectionConfig();
        config.RiskWeights = new RiskWeightsConfig
        {
            CoverageGap      = 0.40, // default 0.30 → sum becomes 1.10
            MutationSurvival = 0.25,
            Complexity       = 0.15,
            CallGraph        = 0.10,
            Churn            = 0.10,
            TestGap          = 0.10
        };

        var result = service.ValidateWeights(RequestFor([], config));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("total"));
    }

    // ── ScoreAsync ────────────────────────────────────────────────────────────

    /// <summary>
    /// ScoreAsync re-runs weight validation internally and throws InvalidOperationException
    /// if the weights are invalid, so callers do not need to call ValidateWeights separately.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_InvalidWeights_ThrowsInvalidOperationException()
    {
        var service = BuildService(providerScore: 0.5);
        var config = ValidTargetSelectionConfig();
        config.RiskWeights = new RiskWeightsConfig { CoverageGap = 0.005 }; // below minimum

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ScoreAsync(RequestFor([MakeCandidate(1)], config)));
    }

    /// <summary>
    /// When all six providers return 1.0, the weighted sum equals 1.0 × 100 = 100.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_AllProvidersReturnMax_RiskScoreIs100()
    {
        var service = BuildService(providerScore: 1.0);
        var candidate = MakeCandidate(id: 1);

        var results = await service.ScoreAsync(RequestFor([candidate]));

        var single = Assert.Single(results);
        Assert.Equal(100.0, single.RiskScore, precision: 3);
    }

    /// <summary>
    /// Results are ordered by RiskScore descending so the highest-risk candidate
    /// appears first, regardless of insertion order.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_ResultsSortedByRiskScoreDescending()
    {
        // Only CoverageGap differs per candidate; all other factors return 0.0.
        // CoverageGap weight = 0.30, so RiskScore = coverageGapScore × 30.
        var service = BuildServiceWithVariableCoverageGap(new Dictionary<int, double>
        {
            [1] = 0.1,  // RiskScore = 3.0
            [2] = 1.0,  // RiskScore = 30.0
            [3] = 0.5   // RiskScore = 15.0
        });

        var results = await service.ScoreAsync(RequestFor(
        [
            MakeCandidate(1), MakeCandidate(2), MakeCandidate(3)
        ]));

        Assert.Equal(3, results.Count);
        Assert.Equal(2, results[0].MemberId); // highest coverage-gap score
        Assert.Equal(3, results[1].MemberId);
        Assert.Equal(1, results[2].MemberId);
    }

    /// <summary>
    /// When two candidates have the same RiskScore, they are ordered by MemberId ascending
    /// as a stable tie-breaker.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_TiedScores_ThenByMemberIdAscending()
    {
        // All factors return the same fixed score → identical RiskScore for both candidates
        var service = BuildService(providerScore: 0.5);

        var results = await service.ScoreAsync(RequestFor(
        [
            MakeCandidate(id: 5), MakeCandidate(id: 2)
        ]));

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results[0].MemberId); // lower ID first on tie
        Assert.Equal(5, results[1].MemberId);
    }

    /// <summary>
    /// When FailOnMissingRiskInputs is true and a factor has no registered provider,
    /// ScoreAsync throws InvalidOperationException naming the missing factor.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_MissingProviderWithFailOnMissingEnabled_Throws()
    {
        // CoverageGap provider omitted
        var service = new RiskScoringService([
            new FixedScoreProvider(RiskFactorKind.MutationSurvival, 0.5),
            new FixedScoreProvider(RiskFactorKind.Complexity,       0.5),
            new FixedScoreProvider(RiskFactorKind.CallGraph,        0.5),
            new FixedScoreProvider(RiskFactorKind.Churn,            0.5),
            new FixedScoreProvider(RiskFactorKind.TestGap,          0.5),
        ]);
        var config = ValidTargetSelectionConfig();
        config.FailOnMissingRiskInputs = true;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ScoreAsync(RequestFor([MakeCandidate(1)], config)));
        Assert.Contains("CoverageGap", ex.Message);
    }

    /// <summary>
    /// When FailOnMissingRiskInputs is false and CoverageGap has no registered provider,
    /// that factor is silently scored as 0.0 and scoring completes without throwing.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_MissingProviderWithFailOnMissingDisabled_ScoresZeroForMissingFactor()
    {
        var service = new RiskScoringService([
            new FixedScoreProvider(RiskFactorKind.MutationSurvival, 0.5),
            new FixedScoreProvider(RiskFactorKind.Complexity,       0.5),
            new FixedScoreProvider(RiskFactorKind.CallGraph,        0.5),
            new FixedScoreProvider(RiskFactorKind.Churn,            0.5),
            new FixedScoreProvider(RiskFactorKind.TestGap,          0.5),
            // CoverageGap missing
        ]);
        var config = ValidTargetSelectionConfig();
        config.FailOnMissingRiskInputs = false;

        var results = await service.ScoreAsync(RequestFor([MakeCandidate(1)], config));

        var single = Assert.Single(results);
        Assert.Equal(0.0, single.FactorScores[RiskFactorKind.CoverageGap]);
    }

    /// <summary>
    /// Provider evidence strings are included in SelectionReason, prefixed with the factor
    /// name and a colon, separated by semicolons. Empty evidence is omitted.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_EvidenceFormattedWithFactorPrefixAndColonSeparator()
    {
        var service = new RiskScoringService([
            new FixedScoreProvider(RiskFactorKind.CoverageGap,      0.5, "high gap"),
            new FixedScoreProvider(RiskFactorKind.MutationSurvival, 0.5, ""),   // omitted
            new FixedScoreProvider(RiskFactorKind.Complexity,       0.5, "complex"),
            new FixedScoreProvider(RiskFactorKind.CallGraph,        0.5, ""),
            new FixedScoreProvider(RiskFactorKind.Churn,            0.5, ""),
            new FixedScoreProvider(RiskFactorKind.TestGap,          0.5, ""),
        ]);

        var results = await service.ScoreAsync(RequestFor([MakeCandidate(1)]));

        var reason = Assert.Single(results).SelectionReason;
        Assert.Contains("CoverageGap: high gap", reason);
        Assert.Contains("Complexity: complex", reason);
        Assert.DoesNotContain("MutationSurvival", reason); // empty evidence omitted
    }

    /// <summary>
    /// Passing an empty candidate list returns an empty result list without throwing.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_EmptyCandidateList_ReturnsEmptyList()
    {
        var service = BuildService(providerScore: 0.5);

        var results = await service.ScoreAsync(RequestFor([]));

        Assert.Empty(results);
    }

    /// <summary>
    /// The FactorScores dictionary in each result contains all six factor kinds with their
    /// normalised scores, and the Weights dictionary reflects the config weights.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScoreAsync_FactorScoresAndWeightsDictionariesPopulated()
    {
        var service = BuildService(providerScore: 0.7);

        var results = await service.ScoreAsync(RequestFor([MakeCandidate(1)]));

        var score = Assert.Single(results);
        Assert.Equal(6, score.FactorScores.Count);
        Assert.All(score.FactorScores.Values, s => Assert.Equal(0.7, s, precision: 3));
        Assert.Equal(6, score.Weights.Count);
        Assert.Equal(0.30, score.Weights[RiskFactorKind.CoverageGap], precision: 3);
        Assert.Equal(0.25, score.Weights[RiskFactorKind.MutationSurvival], precision: 3);
    }

    // ─── Infrastructure ────────────────────────────────────────────────────────

    private static RiskScoringService BuildService(double providerScore) =>
        new([
            new FixedScoreProvider(RiskFactorKind.CoverageGap,      providerScore),
            new FixedScoreProvider(RiskFactorKind.MutationSurvival, providerScore),
            new FixedScoreProvider(RiskFactorKind.Complexity,        providerScore),
            new FixedScoreProvider(RiskFactorKind.CallGraph,         providerScore),
            new FixedScoreProvider(RiskFactorKind.Churn,             providerScore),
            new FixedScoreProvider(RiskFactorKind.TestGap,           providerScore),
        ]);

    /// <summary>
    /// Service where only CoverageGap scores vary per candidate; all other factors return 0.
    /// This lets tests verify sort order by controlling the dominant weight (0.30).
    /// </summary>
    private static RiskScoringService BuildServiceWithVariableCoverageGap(
        IReadOnlyDictionary<int, double> coverageGapByMemberId) =>
        new([
            new VariableScoreProvider(RiskFactorKind.CoverageGap, coverageGapByMemberId),
            new FixedScoreProvider(RiskFactorKind.MutationSurvival, 0.0),
            new FixedScoreProvider(RiskFactorKind.Complexity,       0.0),
            new FixedScoreProvider(RiskFactorKind.CallGraph,        0.0),
            new FixedScoreProvider(RiskFactorKind.Churn,            0.0),
            new FixedScoreProvider(RiskFactorKind.TestGap,          0.0),
        ]);

    private static TargetSelectionConfig ValidTargetSelectionConfig() => new()
    {
        RiskWeights = new RiskWeightsConfig
        {
            CoverageGap      = 0.30,
            MutationSurvival = 0.25,
            Complexity       = 0.15,
            CallGraph        = 0.10,
            Churn            = 0.10,
            TestGap          = 0.10
        },
        FailOnMissingRiskInputs = true
    };

    private static RiskScoringRequest RequestFor(
        IReadOnlyList<MemberModel> candidates,
        TargetSelectionConfig? config = null) =>
        new(candidates, config ?? ValidTargetSelectionConfig());

    private static MemberModel MakeCandidate(int id, string name = "Method") =>
        new(
            attributes: [],
            modifiers: ["public"],
            testCategories: [],
            location: new Location(id, 0, id, 0),
            id: id,
            name: $"{name}{id}",
            kind: "method");

    // ─── Test doubles ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the same <paramref name="score"/> for every candidate.
    /// </summary>
    private sealed class FixedScoreProvider(
        RiskFactorKind kind,
        double score,
        string evidence = "")
        : IRiskFactorProvider
    {
        public RiskFactorKind Factor => kind;

        public Task<RiskFactorScore> ScoreAsync(
            MemberModel candidate,
            CancellationToken ct = default)
            => Task.FromResult(new RiskFactorScore(kind, score, evidence));
    }

    /// <summary>
    /// Returns a per-candidate score looked up by <see cref="MemberModel.Id"/>;
    /// falls back to 0.0 for unregistered member IDs.
    /// </summary>
    private sealed class VariableScoreProvider(
        RiskFactorKind kind,
        IReadOnlyDictionary<int, double> scoresByMemberId)
        : IRiskFactorProvider
    {
        public RiskFactorKind Factor => kind;

        public Task<RiskFactorScore> ScoreAsync(
            MemberModel candidate,
            CancellationToken ct = default)
        {
            var score = scoresByMemberId.TryGetValue(candidate.Id, out var s) ? s : 0.0;
            return Task.FromResult(new RiskFactorScore(kind, score, ""));
        }
    }
}
