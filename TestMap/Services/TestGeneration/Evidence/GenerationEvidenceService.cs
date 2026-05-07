using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Entities.MutationTesting;
using TestMap.Rules.Generation;

namespace TestMap.Services.TestGeneration.Evidence;

public sealed class GenerationEvidenceService : IGenerationEvidenceService
{
    private readonly TestMapDbContext _dbContext;

    public GenerationEvidenceService(TestMapDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GenerationEvidencePackage> BuildAsync(
        GenerationEvidenceOptions options,
        CancellationToken cancellationToken = default)
    {
        var methodId = options.CandidateContext.Method.MemberId;
        var ruleDecisions = new List<Models.Rules.RuleDecisionRecord>
        {
            GenerationEvidenceDecisionEngine.IncludeProjectContext(methodId)
        };

        CoverageEvidence? coverage = null;
        MutationEvidence? mutation = null;

        if (options.Approach == TestGenerationApproach.Naive)
        {
            ruleDecisions.Add(GenerationEvidenceDecisionEngine.SuppressMetricsForNaiveApproach(options.Approach));
        }
        else if (options.Approach == TestGenerationApproach.MetricsDriven)
        {
            if (ShouldIncludeCoverage(options.MetricsPath))
            {
                var coverageEvidence = await BuildCoverageEvidenceAsync(methodId, cancellationToken);
                coverage = coverageEvidence.Evidence;
                ruleDecisions.Add(coverageEvidence.Evidence.Gaps.Count > 0
                    ? GenerationEvidenceDecisionEngine.IncludeCoverageEvidence(
                        coverageEvidence.Evidence.Gaps.Count,
                        options.MetricsPath)
                    : GenerationEvidenceDecisionEngine.CoverageEvidenceUnavailable(options.MetricsPath));
            }

            if (ShouldIncludeMutation(options.MetricsPath))
            {
                mutation = await BuildMutationEvidenceAsync(methodId, cancellationToken);
                ruleDecisions.Add(mutation.SurvivingMutants.Count > 0
                    ? GenerationEvidenceDecisionEngine.IncludeMutationEvidence(
                        mutation.SurvivingMutants.Count,
                        options.MetricsPath)
                    : GenerationEvidenceDecisionEngine.MutationEvidenceUnavailable(options.MetricsPath));
            }
        }

        return new GenerationEvidencePackage
        {
            Objective = options.Objective,
            Approach = options.Approach,
            MetricsPath = options.MetricsPath,
            CandidateContext = options.CandidateContext,
            StrategyInstruction = BuildStrategyInstruction(options.Approach, options.MetricsPath),
            Coverage = coverage,
            Mutation = mutation,
            RuleDecisions = ruleDecisions
        };
    }

    private async Task<(CoverageEvidence Evidence, MemberCoverageEntity? Coverage)> BuildCoverageEvidenceAsync(
        int methodId,
        CancellationToken cancellationToken)
    {
        var latestCoverage = await _dbContext.MemberCoverages
            .AsNoTracking()
            .Where(x => x.MemberId == methodId)
            .OrderByDescending(x => x.CoverageReportId)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var coverageReportId = latestCoverage?.CoverageReportId;
        var gaps = coverageReportId.HasValue
            ? await _dbContext.CoverageGaps
                .AsNoTracking()
                .Where(x => x.MemberId == methodId && x.CoverageReportId == coverageReportId.Value)
                .OrderBy(x => x.LineNumber)
                .ThenBy(x => x.GapKind)
                .ToListAsync(cancellationToken)
            : [];

        return (new CoverageEvidence
        {
            CurrentLineCoverage = latestCoverage?.LineRate,
            CurrentBranchCoverage = latestCoverage?.BranchRate,
            CoverageReportId = coverageReportId?.ToString() ?? string.Empty,
            Summary = gaps.Count == 0
                ? "No line-level coverage gap data is available for this method."
                : $"Coverage evidence includes {gaps.Count} gap(s).",
            Gaps = gaps.Select(ToCoverageGapEvidence).ToList()
        }, latestCoverage);
    }

    private async Task<MutationEvidence> BuildMutationEvidenceAsync(
        int methodId,
        CancellationToken cancellationToken)
    {
        var mutants = await _dbContext.Mutants
            .AsNoTracking()
            .Where(x => x.MemberId == methodId)
            .Where(x => x.Status == "Survived" || x.Status == "NoCoverage")
            .ToListAsync(cancellationToken);
        mutants = mutants
            .OrderBy(x => x.Location.StartLineNumber)
            .ThenBy(x => x.StrykerMutantId)
            .ToList();

        return new MutationEvidence
        {
            Summary = mutants.Count == 0
                ? "No surviving or no-coverage mutants are available for this method."
                : $"Mutation evidence includes {mutants.Count} surviving or no-coverage mutant(s).",
            SurvivingMutants = mutants.Select(ToSurvivingMutantEvidence).ToList()
        };
    }

    private static CoverageGapEvidence ToCoverageGapEvidence(CoverageGapEntity gap)
    {
        return new CoverageGapEvidence
        {
            LineNumber = gap.LineNumber,
            GapKind = gap.GapKind,
            Hits = gap.Hits,
            ConditionCoverage = gap.ConditionCoverage,
            SourceText = gap.SourceText
        };
    }

    private static SurvivingMutantEvidence ToSurvivingMutantEvidence(MutantEntity mutant)
    {
        return new SurvivingMutantEvidence
        {
            MutantId = string.IsNullOrWhiteSpace(mutant.StrykerMutantId)
                ? mutant.Id.ToString()
                : mutant.StrykerMutantId,
            MutatorName = mutant.MutatorName,
            ReplacementCode = mutant.Replacement,
            StartLine = mutant.Location.StartLineNumber,
            EndLine = mutant.Location.EndLineNumber,
            Status = mutant.Status,
            StatusReason = mutant.StatusReason,
            CoveringTests = mutant.CoveredBy
        };
    }

    private static bool ShouldIncludeCoverage(MetricsDrivenPath? metricsPath)
    {
        return metricsPath is MetricsDrivenPath.Coverage or MetricsDrivenPath.CoverageAndMutation;
    }

    private static bool ShouldIncludeMutation(MetricsDrivenPath? metricsPath)
    {
        return metricsPath is MetricsDrivenPath.Mutation or MetricsDrivenPath.CoverageAndMutation;
    }

    private static bool IsUndetectedMutantStatus(string status)
    {
        return status.Equals("Survived", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("NoCoverage", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildStrategyInstruction(TestGenerationApproach approach, MetricsDrivenPath? metricsPath)
    {
        return approach switch
        {
            TestGenerationApproach.Naive =>
                "Generate a useful test for the target method without explicit coverage or mutation evidence.",
            TestGenerationApproach.MetricsDriven when metricsPath == MetricsDrivenPath.Coverage =>
                "Generate a test that targets the supplied coverage gaps.",
            TestGenerationApproach.MetricsDriven when metricsPath == MetricsDrivenPath.Mutation =>
                "Generate a test that distinguishes original behavior from surviving mutants.",
            TestGenerationApproach.MetricsDriven when metricsPath == MetricsDrivenPath.CoverageAndMutation =>
                "Generate a test that targets supplied coverage gaps and surviving mutants.",
            _ =>
                "Generate a focused test using the available project context."
        };
    }
}
