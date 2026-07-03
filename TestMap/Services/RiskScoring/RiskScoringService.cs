using TestMap.Models.RiskScoring;

namespace TestMap.Services.RiskScoring;

public class RiskScoringService(IEnumerable<IRiskFactorProvider> riskFactorProviders) : IRiskScoringService
{
    private const double MinimumWeight = 0.01;
    private const double MaximumWeight = 0.99;
    private const double WeightTotalTolerance = 0.0001;

    public RiskScoringValidationResult ValidateWeights(RiskScoringRequest request)
    {
        var errors = new List<string>();
        var weights = request.TargetSelectionConfig.RiskWeights.ToDictionary();

        foreach (var (factor, weight) in weights)
            if (weight is < MinimumWeight or > MaximumWeight)
                errors.Add($"Risk weight '{factor}' must be between {MinimumWeight:0.00} and {MaximumWeight:0.00}.");

        var total = weights.Values.Sum();
        if (Math.Abs(total - 1.0) > WeightTotalTolerance)
            errors.Add($"Risk weights must total 1.0. Current total is {total:0.####}.");

        return errors.Count == 0
            ? RiskScoringValidationResult.Success()
            : new RiskScoringValidationResult(false, errors);
    }

    public async Task<IReadOnlyList<MethodRiskScore>> ScoreAsync(
        RiskScoringRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateWeights(request);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

        var providersByFactor = riskFactorProviders.ToDictionary(x => x.Factor);
        var weights = request.TargetSelectionConfig.RiskWeights.ToDictionary();
        var results = new List<MethodRiskScore>();

        foreach (var candidate in request.CandidateMembers)
        {
            var factorScores = new Dictionary<Models.Configuration.Testing.Generation.RiskFactorKind, double>();
            var evidence = new List<string>();

            foreach (var (factor, weight) in weights)
            {
                if (!providersByFactor.TryGetValue(factor, out var provider))
                {
                    if (request.TargetSelectionConfig.FailOnMissingRiskInputs)
                        throw new InvalidOperationException($"No risk factor provider is registered for '{factor}'.");

                    factorScores[factor] = 0.0;
                    continue;
                }

                var factorScore = await provider.ScoreAsync(candidate, cancellationToken);
                factorScores[factor] = Clamp01(factorScore.Score);
                if (!string.IsNullOrWhiteSpace(factorScore.Evidence)) evidence.Add($"{factor}: {factorScore.Evidence}");
            }

            var weightedScore = factorScores.Sum(x => x.Value * weights[x.Key]);
            results.Add(new MethodRiskScore
            {
                MemberId = candidate.Id,
                RiskScore = weightedScore * 100,
                FactorScores = factorScores,
                Weights = weights.ToDictionary(x => x.Key, x => x.Value),
                SelectionReason = string.Join("; ", evidence),
                CreatedAt = DateTime.UtcNow
            });
        }

        return results
            .OrderByDescending(x => x.RiskScore)
            .ThenBy(x => x.MemberId)
            .ToList();
    }

    private static double Clamp01(double value)
    {
        return Math.Min(1.0, Math.Max(0.0, value));
    }
}