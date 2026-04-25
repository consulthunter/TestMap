using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Services.TestGeneration.TargetSelection.Strategies;

namespace TestMap.Services.TestGeneration.TargetSelection;

public sealed class CandidateMethodSelector
{
    private readonly ProjectContext _context;
    private readonly TestMapDbContext _dbContext;
    private readonly TestMapConfig _config;
    private readonly IReadOnlyDictionary<TargetSelectionStrategy, ICandidateSelectionStrategy> _strategies;

    public CandidateMethodSelector(
        ProjectContext context,
        TestMapDbContext dbContext,
        TestMapConfig config,
        IEnumerable<ICandidateSelectionStrategy> strategies)
    {
        _context = context;
        _dbContext = dbContext;
        _config = config;
        _strategies = strategies.ToDictionary(x => x.Strategy);
    }

    public async Task<List<CandidateMethod>> SelectAsync(
        ExperimentConfig config,
        CancellationToken cancellationToken = default)
    {
        _context.Project.Logger?.Information(
            "Selecting candidate methods with coverage between {MinCoverage:P} and {MaxCoverage:P}",
            config.MinCoverageThreshold,
            config.MaxCoverageThreshold);

        var targetSelection = ResolveTargetSelectionConfig(config);
        var effectiveLimit = Math.Max(1, config.CandidateLimit);
        var usesScoredSelection = targetSelection.Strategy is TargetSelectionStrategy.RiskWeighted
            or TargetSelectionStrategy.MetricDrivenImprovement
            or TargetSelectionStrategy.TestSuiteImprovements;
        var candidatePoolLimit = usesScoredSelection
            ? Math.Clamp(Math.Max(effectiveLimit * 20, targetSelection.CandidateLimit * 2), effectiveLimit, 1000)
            : effectiveLimit;

        var candidateRows = await LoadCoverageCandidatePoolAsync(
            config.MinCoverageThreshold,
            config.MaxCoverageThreshold,
            candidatePoolLimit,
            cancellationToken);

        if (!_strategies.TryGetValue(targetSelection.Strategy, out var strategy))
            throw new InvalidOperationException(
                $"No candidate selection strategy is registered for '{targetSelection.Strategy}'.");

        var candidateMethods = await strategy.SelectAsync(
            new CandidateSelectionContext
            {
                ExperimentConfiguration = config,
                TargetSelection = targetSelection,
                SelectionTime = DateTime.UtcNow,
                EffectiveLimit = effectiveLimit
            },
            candidateRows,
            cancellationToken);

        _context.Project.Logger?.Information("Found {Count} candidate methods", candidateMethods.Count);
        return candidateMethods.ToList();
    }

    private TargetSelectionConfig ResolveTargetSelectionConfig(ExperimentConfig config)
    {
        var configuredSelection = _config.TestingConfig.GenerationConfig.TargetSelection;
        var strategy = config.CandidateSelectionStrategy ?? configuredSelection.Strategy;

        return new TargetSelectionConfig
        {
            Strategy = strategy,
            CandidateLimit = configuredSelection.CandidateLimit,
            RiskWeights = configuredSelection.RiskWeights,
            MetricDrivenImprovement = configuredSelection.MetricDrivenImprovement,
            TestSuiteImprovement = configuredSelection.TestSuiteImprovement,
            FailOnMissingRiskInputs = configuredSelection.FailOnMissingRiskInputs
        };
    }

    private async Task<List<CandidateSelectionRow>> LoadCoverageCandidatePoolAsync(
        double minCoverageThreshold,
        double maxCoverageThreshold,
        int candidatePoolLimit,
        CancellationToken cancellationToken)
    {
        var candidateData = await (
                from member in _dbContext.Members.AsNoTracking()
                let selectedCoverage = (
                        from coverage in _dbContext.MemberCoverages
                        join report in _dbContext.CoverageReports on coverage.CoverageReportId equals report.Id
                        where coverage.MemberId == member.Id
                              && coverage.LineRate >= minCoverageThreshold
                              && coverage.LineRate <= maxCoverageThreshold
                        orderby report.Timestamp descending, report.CreatedAt descending, coverage.Id descending
                        select new
                        {
                            coverage.LineRate,
                            coverage.Complexity
                        })
                    .FirstOrDefault()
                where !member.IsTestMember
                      && !member.IsGenerated
                      && member.Kind == "method"
                      && selectedCoverage != null
                orderby selectedCoverage.LineRate ascending, selectedCoverage.Complexity descending
                select new
                {
                    member.Id,
                    member.Name,
                    member.FullString,
                    selectedCoverage.LineRate,
                    selectedCoverage.Complexity
                })
            .Take(candidatePoolLimit)
            .ToListAsync(cancellationToken);

        return candidateData
            .Select(x => new CandidateSelectionRow(
                x.Id,
                x.Name,
                x.FullString,
                x.LineRate,
                x.Complexity))
            .ToList();
    }
}
