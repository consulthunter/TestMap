using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Experiment;

namespace TestMap.Services.Experiment.Reporting;

/// <summary>
/// Service for analyzing experiment results and generating reports.
/// </summary>
public class ExperimentAnalysisService : IExperimentAnalysisService
{
    private readonly ProjectContext _context;
    private readonly ExperimentRunRepository _experimentRunRepo;
    private readonly CandidateMethodRepository _candidateMethodRepo;
    private readonly GenerationAttemptRepository _attemptRepo;
    private readonly GenerationStepRepository _stepRepo;
    private readonly TestExecutionRepository _executionRepo;
    private readonly TestMapDbContext _dbContext;

    public ExperimentAnalysisService(
        ProjectContext context,
        ExperimentRunRepository experimentRunRepo,
        CandidateMethodRepository candidateMethodRepo,
        GenerationAttemptRepository attemptRepo,
        GenerationStepRepository stepRepo,
        TestExecutionRepository executionRepo,
        TestMapDbContext dbContext)
    {
        _context = context;
        _experimentRunRepo = experimentRunRepo;
        _candidateMethodRepo = candidateMethodRepo;
        _attemptRepo = attemptRepo;
        _stepRepo = stepRepo;
        _executionRepo = executionRepo;
        _dbContext = dbContext;
    }

    public async Task<ExperimentAnalysisReport> AnalyzeExperimentAsync(
        int experimentRunId,
        CancellationToken cancellationToken = default)
    {
        _context.Project.Logger?.Information($"Analyzing experiment {experimentRunId}");

        var experimentRun = await _experimentRunRepo.GetByIdAsync(experimentRunId);
        if (experimentRun == null) throw new ArgumentException($"Experiment run {experimentRunId} not found");

        var candidateMethods = await _candidateMethodRepo.GetByExperimentAsync(experimentRunId);
        var allAttempts = new List<GenerationAttempt>();

        foreach (var method in candidateMethods)
        {
            var attempts = await _attemptRepo.GetByCandidateMethodAsync(method.Id);
            allAttempts.AddRange(attempts);

            // Load execution results
            foreach (var attempt in attempts)
                attempt.TestExecution = await _executionRepo.GetByAttemptAsync(attempt.Id);
        }

        // Calculate provider performance
        var providerPerformance = CalculateProviderPerformance(allAttempts);

        // Calculate strategy performance
        var strategyPerformance = CalculateStrategyPerformance(allAttempts);

        // Calculate overall summary
        var summary = CalculateSummary(
            candidateMethods,
            allAttempts,
            providerPerformance,
            strategyPerformance);
        var detailedResults = await BuildDetailedResultsAsync(
            candidateMethods,
            true,
            cancellationToken);
        var projects = BuildProjectResults(detailedResults);

        return new ExperimentAnalysisReport
        {
            ExperimentRun = experimentRun,
            ProviderPerformance = providerPerformance,
            StrategyPerformance = strategyPerformance,
            Summary = summary,
            Projects = projects,
            DetailedResults = detailedResults
        };
    }

    public async Task ExportToCsvAsync(
        int experimentRunId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _context.Project.Logger?.Information($"Exporting experiment {experimentRunId} to CSV: {outputPath}");

        var experimentRun = await _experimentRunRepo.GetByIdAsync(experimentRunId);
        if (experimentRun == null) throw new ArgumentException($"Experiment run {experimentRunId} not found");

        var config = JsonSerializer.Deserialize<ExperimentConfiguration>(experimentRun.ConfigurationJson);
        var includeDetailedErrors = config?.IncludeDetailedErrors ?? true;
        var candidateMethods = await _candidateMethodRepo.GetByExperimentAsync(experimentRunId);

        var detailedResults = await BuildDetailedResultsAsync(
            candidateMethods,
            includeDetailedErrors,
            cancellationToken);
        var csv = new StringBuilder();
        csv.AppendLine(
            "Owner,Repo,Branch,CommitHash,SourceProjectName,SourceProjectPath,TestProjectName,TestProjectPath,Method,ExampleTestName,GeneratedTestName,ExampleTestExecutionTimeMs,GeneratedTestExecutionTimeMs,ExampleTestCodeMetrics,GeneratedTestCodeMetrics,ExampleTestSmells,GeneratedTestSmells,Provider,Strategy,AttemptNumber,CompilationSuccess,TestPassed,CoverageBefore,CoverageAfter,CoverageImprovement,BaselineMutationScore,MutationScoreAfter,MutationScoreImprovement,TotalTokens,DurationSeconds,ErrorLogs");

        foreach (var row in detailedResults)
            csv.AppendLine(
                $"{EscapeCsv(row.Owner)}," +
                $"{EscapeCsv(row.Repo)}," +
                $"{EscapeCsv(row.Branch ?? string.Empty)}," +
                $"{EscapeCsv(row.CommitHash ?? string.Empty)}," +
                $"{EscapeCsv(row.SourceProjectName ?? string.Empty)}," +
                $"{EscapeCsv(row.SourceProjectPath ?? string.Empty)}," +
                $"{EscapeCsv(row.TestProjectName ?? string.Empty)}," +
                $"{EscapeCsv(row.TestProjectPath ?? string.Empty)}," +
                $"{EscapeCsv(row.Method)}," +
                $"{EscapeCsv(row.ExampleTestName ?? string.Empty)}," +
                $"{EscapeCsv(row.GeneratedTestName ?? string.Empty)}," +
                $"{FormatNullableDouble(row.ExampleTestExecutionTimeMs)}," +
                $"{FormatNullableDouble(row.GeneratedTestExecutionTimeMs)}," +
                $"{EscapeCsv(row.ExampleTestCodeMetrics)}," +
                $"{EscapeCsv(row.GeneratedTestCodeMetrics)}," +
                $"{EscapeCsv(row.ExampleTestSmells)}," +
                $"{EscapeCsv(row.GeneratedTestSmells)}," +
                $"{row.Provider}," +
                $"{row.Strategy}," +
                $"{row.AttemptNumber}," +
                $"{row.CompilationSuccess}," +
                $"{row.TestPassed}," +
                $"{row.CoverageBefore:F4}," +
                $"{row.CoverageAfter:F4}," +
                $"{row.CoverageImprovement:F4}," +
                $"{FormatNullableDouble(row.BaselineMutationScore)}," +
                $"{FormatNullableDouble(row.MutationScoreAfter)}," +
                $"{FormatNullableDouble(row.MutationScoreImprovement)}," +
                $"{row.TotalTokens}," +
                $"{row.DurationSeconds:F2}," +
                $"{EscapeCsv(row.ErrorLogs)}");

        await File.WriteAllTextAsync(outputPath, csv.ToString(), cancellationToken);

        _context.Project.Logger?.Information($"CSV export complete: {outputPath}");
    }

    public async Task ExportToJsonAsync(
        int experimentRunId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _context.Project.Logger?.Information($"Exporting experiment {experimentRunId} to JSON: {outputPath}");

        var report = await AnalyzeExperimentAsync(experimentRunId, cancellationToken);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(outputPath, json, cancellationToken);

        _context.Project.Logger?.Information($"JSON export complete: {outputPath}");
    }

    #region Helper Methods

    private List<ProviderPerformance> CalculateProviderPerformance(List<GenerationAttempt> attempts)
    {
        return attempts
            .GroupBy(a => a.Provider)
            .Select(g => new ProviderPerformance
            {
                Provider = g.Key,
                TotalAttempts = g.Count(),
                SuccessfulTests = g.Count(a => a.TestExecution?.TestPassed ?? false),
                AverageCoverageImprovement = g.Average(a => a.TestExecution?.CoverageImprovement ?? 0.0),
                TotalTokensUsed = g.Sum(a => a.TotalTokensUsed),
                AverageDurationSeconds = g.Average(a => a.TotalDurationSeconds),
                CompilationFailures = g.Count(a => !(a.TestExecution?.CompilationSuccess ?? false)),
                TestFailures = g.Count(a => !(a.TestExecution?.TestPassed ?? false))
            })
            .OrderByDescending(p => p.SuccessRate)
            .ToList();
    }

    private List<StrategyPerformance> CalculateStrategyPerformance(List<GenerationAttempt> attempts)
    {
        return attempts
            .GroupBy(a => a.Strategy)
            .Select(g => new StrategyPerformance
            {
                Strategy = g.Key,
                TotalAttempts = g.Count(),
                SuccessfulTests = g.Count(a => a.TestExecution?.TestPassed ?? false),
                AverageCoverageImprovement = g.Average(a => a.TestExecution?.CoverageImprovement ?? 0.0),
                TotalTokensUsed = g.Sum(a => a.TotalTokensUsed),
                AverageDurationSeconds = g.Average(a => a.TotalDurationSeconds)
            })
            .OrderByDescending(s => s.SuccessRate)
            .ToList();
    }

    private ExperimentSummary CalculateSummary(
        List<CandidateMethod> methods,
        List<GenerationAttempt> attempts,
        List<ProviderPerformance> providerPerformance,
        List<StrategyPerformance> strategyPerformance)
    {
        var bestProvider = providerPerformance.FirstOrDefault();
        var bestStrategy = strategyPerformance.FirstOrDefault();

        var experimentStart = attempts.Min(a => a.StartedAt);
        var experimentEnd = attempts.Max(a => a.CompletedAt ?? a.StartedAt);
        var totalDuration = (experimentEnd - experimentStart).TotalSeconds;

        return new ExperimentSummary
        {
            TotalMethods = methods.Count,
            TotalAttempts = attempts.Count,
            TotalSuccesses = attempts.Count(a => a.TestExecution?.TestPassed ?? false),
            TotalTokensUsed = attempts.Sum(a => a.TotalTokensUsed),
            TotalDurationSeconds = totalDuration,
            BestProvider = bestProvider?.Provider,
            BestStrategy = bestStrategy?.Strategy,
            BestProviderSuccessRate = bestProvider?.SuccessRate ?? 0.0,
            BestStrategySuccessRate = bestStrategy?.SuccessRate ?? 0.0
        };
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }

    private static string FormatNullableDouble(double? value)
    {
        return value.HasValue ? value.Value.ToString("F2") : string.Empty;
    }

    private async Task<List<ExperimentResultRow>> BuildDetailedResultsAsync(
        List<CandidateMethod> candidateMethods,
        bool includeDetailedErrors,
        CancellationToken cancellationToken)
    {
        var owner = _context.Project.Owner;
        var repo = _context.Project.RepoName;
        var branch = _context.Project.Branch;
        var commitHash = _context.Project.Commit ?? _context.Project.LastAnalyzedCommit ?? _context.CurrentCommit;
        var rows = new List<ExperimentResultRow>();
        var candidateProjectInfo = await LoadCandidateProjectInfoAsync(candidateMethods, cancellationToken);

        foreach (var method in candidateMethods)
        {
            var attempts = await _attemptRepo.GetByCandidateMethodAsync(method.Id, cancellationToken);
            var projectInfo = candidateProjectInfo.GetValueOrDefault(method.Id);
            var exampleDurationMs = await GetLatestTestDurationMsAsync(
                method.ExistingTestMethodName,
                method.ExistingTestMemberId,
                cancellationToken);
            var exampleCodeMetrics = await GetMemberCodeMetricsSummaryAsync(
                method.ExistingTestMemberId,
                cancellationToken);
            var exampleTestSmells = await GetTestSmellSummaryAsync(
                method.ExistingTestMethodName,
                method.ExistingTestMemberId,
                cancellationToken);

            foreach (var attempt in attempts)
            {
                var execution = await _executionRepo.GetByAttemptAsync(attempt.Id, cancellationToken);
                var generatedTestName = execution?.GeneratedTestMethodName;
                var generatedDurationMs = await GetLatestTestDurationMsAsync(
                    generatedTestName,
                    null,
                    cancellationToken);
                var generatedMemberId = await ResolveLatestTestMemberIdAsync(
                    generatedTestName,
                    cancellationToken);
                var generatedCodeMetrics = await GetMemberCodeMetricsSummaryAsync(
                    generatedMemberId,
                    cancellationToken);
                var generatedTestSmells = await GetTestSmellSummaryAsync(
                    generatedTestName,
                    generatedMemberId,
                    cancellationToken);

                rows.Add(new ExperimentResultRow
                {
                    Owner = owner,
                    Repo = repo,
                    Branch = branch,
                    CommitHash = commitHash,
                    SourceProjectName = projectInfo?.SourceProjectName,
                    SourceProjectPath = projectInfo?.SourceProjectPath,
                    TestProjectName = projectInfo?.TestProjectName,
                    TestProjectPath = projectInfo?.TestProjectPath,
                    Method = method.MethodName,
                    ExampleTestName = method.ExistingTestMethodName,
                    GeneratedTestName = generatedTestName,
                    ExampleTestExecutionTimeMs = exampleDurationMs,
                    GeneratedTestExecutionTimeMs = generatedDurationMs,
                    ExampleTestCodeMetrics = exampleCodeMetrics,
                    GeneratedTestCodeMetrics = generatedCodeMetrics,
                    ExampleTestSmells = exampleTestSmells,
                    GeneratedTestSmells = generatedTestSmells,
                    Provider = attempt.Provider,
                    Strategy = attempt.Strategy,
                    AttemptNumber = attempt.AttemptNumber,
                    CompilationSuccess = execution?.CompilationSuccess ?? false,
                    TestPassed = execution?.TestPassed ?? false,
                    CoverageBefore = method.BaselineCoverage,
                    CoverageAfter = execution?.CoverageAfter ?? 0.0,
                    CoverageImprovement = execution?.CoverageImprovement ?? 0.0,
                    BaselineMutationScore = execution?.BaselineMutationScore,
                    MutationScoreAfter = execution?.MutationScoreAfter,
                    MutationScoreImprovement = execution?.MutationScoreImprovement,
                    TotalTokens = attempt.TotalTokensUsed,
                    DurationSeconds = attempt.TotalDurationSeconds,
                    ErrorLogs = includeDetailedErrors ? execution?.ErrorLogs ?? string.Empty : string.Empty
                });
            }
        }

        return rows;
    }

    private List<ExperimentProjectRow> BuildProjectResults(List<ExperimentResultRow> detailedResults)
    {
        return detailedResults
            .GroupBy(x => new
            {
                Name = x.SourceProjectName ?? string.Empty,
                Path = x.SourceProjectPath ?? string.Empty
            })
            .Select(group => new ExperimentProjectRow
            {
                ProjectName = group.Key.Name,
                ProjectPath = group.Key.Path,
                CandidateMethodCount = group.Select(x => x.Method).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                AttemptCount = group.Count(),
                SuccessfulTests = group.Count(x => x.TestPassed)
            })
            .OrderBy(x => x.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<Dictionary<int, CandidateProjectInfo>> LoadCandidateProjectInfoAsync(
        List<CandidateMethod> candidateMethods,
        CancellationToken cancellationToken)
    {
        var candidateIds = candidateMethods.Select(x => x.Id).ToList();

        return await (
                from candidate in _dbContext.CandidateMethods
                where candidateIds.Contains(candidate.Id)
                join sourceMember in _dbContext.Members on candidate.SourceMemberId equals sourceMember.Id
                join sourceObject in _dbContext.Objects on sourceMember.ObjectEntityId equals sourceObject.Id
                join sourceFile in _dbContext.Files on sourceObject.FileId equals sourceFile.Id
                join sourceProject in _dbContext.CSharpProjects on sourceFile.CSharpProjectId equals sourceProject.Id
                join testMemberLeft in _dbContext.Members on candidate.ExistingTestMemberId equals testMemberLeft.Id
                    into testMembers
                from testMember in testMembers.DefaultIfEmpty()
                join testObjectLeft in _dbContext.Objects on testMember.ObjectEntityId equals testObjectLeft.Id into
                    testObjects
                from testObject in testObjects.DefaultIfEmpty()
                join testFileLeft in _dbContext.Files on testObject.FileId equals testFileLeft.Id into testFiles
                from testFile in testFiles.DefaultIfEmpty()
                join testProjectLeft in _dbContext.CSharpProjects on testFile.CSharpProjectId equals testProjectLeft.Id
                    into testProjects
                from testProject in testProjects.DefaultIfEmpty()
                select new
                {
                    candidate.Id,
                    SourceProjectPath = sourceProject.FilePath,
                    TestProjectPath = testProject != null ? testProject.FilePath : null
                })
            .ToDictionaryAsync(
                x => x.Id,
                x => new CandidateProjectInfo(
                    GetFileStem(x.SourceProjectPath),
                    x.SourceProjectPath,
                    string.IsNullOrWhiteSpace(x.TestProjectPath) ? null : GetFileStem(x.TestProjectPath),
                    x.TestProjectPath),
                cancellationToken);
    }

    private async Task<double?> GetLatestTestDurationMsAsync(
        string? testName,
        int? memberId,
        CancellationToken cancellationToken)
    {
        if (_context.Project.DbId == 0 || (string.IsNullOrWhiteSpace(testName) && !memberId.HasValue)) return null;

        if (memberId.HasValue)
        {
            var byMemberId =
                from testResult in _dbContext.TestResults
                join testRun in _dbContext.TestRuns on testResult.TestRunId equals testRun.Id
                where testRun.ProjectId == _context.Project.DbId
                      && testResult.MethodId == memberId.Value
                orderby testRun.Id descending, testResult.Id descending
                select (double?)testResult.Duration.TotalMilliseconds;

            var resultByMemberId = await byMemberId.FirstOrDefaultAsync(cancellationToken);
            if (resultByMemberId.HasValue) return resultByMemberId;
        }

        if (string.IsNullOrWhiteSpace(testName)) return null;

        var result = await (
                from testResult in _dbContext.TestResults
                join testRun in _dbContext.TestRuns on testResult.TestRunId equals testRun.Id
                where testRun.ProjectId == _context.Project.DbId
                      && (
                          testResult.TestName == testName ||
                          EF.Functions.Like(testResult.TestName, "%." + testName) ||
                          EF.Functions.Like(testResult.TestName, "%+" + testName))
                orderby testRun.Id descending, testResult.Id descending
                select (double?)testResult.Duration.TotalMilliseconds)
            .FirstOrDefaultAsync(cancellationToken);

        return result;
    }

    private async Task<int?> ResolveLatestTestMemberIdAsync(
        string? testName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(testName)) return null;

        return await (
                from member in _dbContext.Members
                where member.IsTestMember
                      && (
                          member.Name == testName ||
                          EF.Functions.Like(member.Name, "%" + testName))
                orderby member.Id descending
                select (int?)member.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> GetMemberCodeMetricsSummaryAsync(
        int? memberId,
        CancellationToken cancellationToken)
    {
        if (!memberId.HasValue) return string.Empty;

        var metric = await _dbContext.CodeMetrics
            .Where(x => x.EntityType == "member" && x.EntityId == memberId.Value)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (metric == null) return string.Empty;

        return string.Join(
            "; ",
            $"MI={metric.MaintainabilityIndex}",
            $"CC={metric.CyclomaticComplexity}",
            $"Coupling={metric.ClassCoupling}",
            $"DIT={metric.DepthOfInheritance}",
            $"SLOC={metric.SourceLinesOfCode}",
            $"ELOC={metric.ExecutableLinesOfCode}");
    }

    private async Task<string> GetTestSmellSummaryAsync(
        string? testName,
        int? memberId,
        CancellationToken cancellationToken)
    {
        if (_context.Project.DbId == 0 && !memberId.HasValue && string.IsNullOrWhiteSpace(testName))
            return string.Empty;

        var query = _dbContext.TestSmells.AsQueryable();

        if (memberId.HasValue)
            query = query.Where(x => x.MemberId == memberId.Value);
        else if (!string.IsNullOrWhiteSpace(testName))
            query = query.Where(x => x.TestMethodName == testName);
        else
            return string.Empty;

        var smells = await query
            .Where(x => _context.Project.DbId == 0 || x.ProjectId == _context.Project.DbId)
            .GroupBy(x => x.SmellName)
            .Select(x => new { Name = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        if (smells.Count == 0) return "None";

        return string.Join("; ", smells.Select(x => $"{x.Name}={x.Count}"));
    }

    private static string GetFileStem(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }

    private sealed record CandidateProjectInfo(
        string SourceProjectName,
        string SourceProjectPath,
        string? TestProjectName,
        string? TestProjectPath);

    #endregion
}