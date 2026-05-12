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
    // Temporary experiment switch. Set to false to restore selection of all non-test source methods.
    private const bool TemporaryPublicMethodCandidatesOnly = true;

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
        var candidateRows = await LoadCoverageCandidatePoolAsync(
            config.MinCoverageThreshold,
            config.MaxCoverageThreshold,
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
                EffectiveLimit = Math.Max(1, config.CandidateLimit)
            },
            candidateRows,
            cancellationToken);

        _context.Project.Logger?.Information(
            "Found {Count} possible candidate method(s) before final configured target selection.",
            candidateMethods.Count);
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
            ContextMappingMode = config.ContextMappingMode ?? configuredSelection.ContextMappingMode,
            RiskWeights = configuredSelection.RiskWeights,
            MetricDrivenImprovement = configuredSelection.MetricDrivenImprovement,
            FailOnMissingRiskInputs = configuredSelection.FailOnMissingRiskInputs
        };
    }

    private async Task<List<CandidateSelectionRow>> LoadCoverageCandidatePoolAsync(
        double minCoverageThreshold,
        double maxCoverageThreshold,
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
                      && (!TemporaryPublicMethodCandidatesOnly ||
                          EF.Functions.Like(member.FullString, "%public %"))
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
