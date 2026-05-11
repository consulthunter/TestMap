using TestMap.App;
using TestMap.Models.Coverage;
using TestMap.Models.Results;
using TestMap.Services.TestExecution.Collection;
using TestMap.Services.TestExecution.Mapping;

namespace TestMap.Services.TestExecution;

public sealed class BuildTestResultCollector
{
    private readonly ProjectContext _context;
    private readonly CollectCoverageResultsService _collectCoverageResultsService;
    private readonly CollectMutationTestingResultsService _collectMutationTestingResultsService;
    private readonly CollectTestResultsService _collectTestResultsService;
    private readonly MapCoverageService _mapCoverageService;
    private readonly MapMutationService _mapMutationService;

    public BuildTestResultCollector(
        ProjectContext context,
        CollectCoverageResultsService collectCoverageResultsService,
        CollectMutationTestingResultsService collectMutationTestingResultsService,
        CollectTestResultsService collectTestResultsService,
        MapCoverageService mapCoverageService,
        MapMutationService mapMutationService)
    {
        _context = context;
        _collectCoverageResultsService = collectCoverageResultsService;
        _collectMutationTestingResultsService = collectMutationTestingResultsService;
        _collectTestResultsService = collectTestResultsService;
        _mapCoverageService = mapCoverageService;
        _mapMutationService = mapMutationService;
    }

    public async Task<BuildTestCollectedResults> CollectAndMapAsync(
        string runId,
        string runDate,
        IReadOnlyCollection<string> mutationTargets)
    {
        var (testResults, testResultRaw) = await _collectTestResultsService.CollectAsync(runId, runDate);
        _context.Project.TestResults = testResults;

        CoverageReportModel? coverageReport;
        string rawCoverageReport;
        string normalizedCoverageReport;

        try
        {
            (coverageReport, rawCoverageReport, normalizedCoverageReport) =
                await _collectCoverageResultsService.CollectAsync(runId);
            _context.Project.CoverageReport = coverageReport;

            if (coverageReport != null) await _mapCoverageService.MapAsync(coverageReport);
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error(ex, "Coverage collection or mapping failed.");
            throw;
        }

        if (mutationTargets.Count == 0)
            return new BuildTestCollectedResults(
                testResults,
                testResultRaw,
                coverageReport,
                rawCoverageReport,
                normalizedCoverageReport,
                string.Empty,
                null,
                []);

        try
        {
            var (mutationReports, rawMutationReports) =
                await _collectMutationTestingResultsService.CollectAsync(runId, mutationTargets.ToList());

            var mutationScores = mutationReports
                .Select(mutationReport => _mapMutationService.CalculateMutationScore(mutationReport))
                .ToList();

            return new BuildTestCollectedResults(
                testResults,
                testResultRaw,
                coverageReport,
                rawCoverageReport,
                normalizedCoverageReport,
                rawMutationReports,
                mutationScores.Count == 0 ? null : mutationScores.Average(),
                mutationReports);
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Error(ex, "Mutation collection or mapping failed.");
            throw;
        }
    }

    public async Task PersistMutationReportsAsync(
        int testRunId,
        IReadOnlyCollection<StrykerMutationResults> mutationReports)
    {
        foreach (var mutationReport in mutationReports)
            await _mapMutationService.MapAsync(mutationReport, testRunId);
    }
}

public sealed record BuildTestCollectedResults(
    List<TestResultModel> TestResults,
    string TestResultRaw,
    CoverageReportModel? CoverageReport,
    string CoverageReportRaw,
    string CoverageReportNormalizedRaw,
    string MutationReportRaw,
    double? MutationScore,
    IReadOnlyCollection<StrykerMutationResults> MutationReports);
