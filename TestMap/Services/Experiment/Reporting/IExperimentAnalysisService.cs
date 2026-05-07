using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment.Reporting;

/// <summary>
/// Service for analyzing experiment results and generating reports.
/// </summary>
public interface IExperimentAnalysisService
{
    /// <summary>
    /// Analyzes experiment results and generates a comprehensive report.
    /// </summary>
    /// <param name="experimentRunId">ID of the experiment run</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis report with metrics and comparisons</returns>
    Task<ExperimentAnalysisReport> AnalyzeExperimentAsync(
        int experimentRunId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports experiment results to CSV format.
    /// </summary>
    /// <param name="experimentRunId">ID of the experiment run</param>
    /// <param name="outputPath">Path to save the CSV file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExportToCsvAsync(
        int experimentRunId,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports experiment results to JSON format.
    /// </summary>
    /// <param name="experimentRunId">ID of the experiment run</param>
    /// <param name="outputPath">Path to save the JSON file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExportToJsonAsync(
        int experimentRunId,
        string outputPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Comprehensive analysis report for an experiment.
/// </summary>
public class ExperimentAnalysisReport
{
    public required ExperimentRun ExperimentRun { get; init; }
    public required List<ProviderPerformance> ProviderPerformance { get; init; }
    public required List<BudgetModePerformance> BudgetModePerformance { get; init; }
    public required ExperimentSummary Summary { get; init; }
    public required List<ExperimentProjectRow> Projects { get; init; }
    public required List<ExperimentResultRow> DetailedResults { get; init; }
}

/// <summary>
/// Performance metrics for a specific AI provider.
/// </summary>
public class ProviderPerformance
{
    public required AiProvider Provider { get; init; }
    public int TotalAttempts { get; init; }
    public int SuccessfulTests { get; init; }
    public double SuccessRate => TotalAttempts > 0 ? (double)SuccessfulTests / TotalAttempts : 0.0;
    public double AverageCoverageImprovement { get; init; }
    public int TotalTokensUsed { get; init; }
    public double AverageTokensPerAttempt => TotalAttempts > 0 ? (double)TotalTokensUsed / TotalAttempts : 0.0;
    public double AverageDurationSeconds { get; init; }
    public int CompilationFailures { get; init; }
    public int TestFailures { get; init; }
}

/// <summary>
/// Performance metrics for a specific generation budget mode.
/// </summary>
public class BudgetModePerformance
{
    public required GenerationBudgetMode BudgetMode { get; init; }
    public int TotalAttempts { get; init; }
    public int SuccessfulTests { get; init; }
    public double SuccessRate => TotalAttempts > 0 ? (double)SuccessfulTests / TotalAttempts : 0.0;
    public double AverageCoverageImprovement { get; init; }
    public int TotalTokensUsed { get; init; }
    public double AverageTokensPerAttempt => TotalAttempts > 0 ? (double)TotalTokensUsed / TotalAttempts : 0.0;
    public double AverageDurationSeconds { get; init; }
}

/// <summary>
/// Overall experiment summary.
/// </summary>
public class ExperimentSummary
{
    public int TotalMethods { get; init; }
    public int TotalAttempts { get; init; }
    public int TotalSuccesses { get; init; }
    public double OverallSuccessRate => TotalAttempts > 0 ? (double)TotalSuccesses / TotalAttempts : 0.0;
    public int TotalTokensUsed { get; init; }
    public double TotalDurationSeconds { get; init; }
    public AiProvider? BestProvider { get; init; }
    public GenerationBudgetMode? BestBudgetMode { get; init; }
    public double BestProviderSuccessRate { get; init; }
    public double BestBudgetModeSuccessRate { get; init; }
}

public class ExperimentResultRow
{
    public required string Owner { get; init; }
    public required string Repo { get; init; }
    public string? Branch { get; init; }
    public string? CommitHash { get; init; }
    public string? SourceProjectName { get; init; }
    public string? SourceProjectPath { get; init; }
    public string? TestProjectName { get; init; }
    public string? TestProjectPath { get; init; }
    public required string Method { get; init; }
    public string? ExampleTestName { get; init; }
    public string? GeneratedTestName { get; init; }
    public double? ExampleTestExecutionTimeMs { get; init; }
    public double? GeneratedTestExecutionTimeMs { get; init; }
    public string SourceMethodCodeMetrics { get; init; } = string.Empty;
    public string ExampleTestCodeMetrics { get; init; } = string.Empty;
    public string GeneratedTestCodeMetrics { get; init; } = string.Empty;
    public string ExampleTestSmells { get; init; } = string.Empty;
    public string GeneratedTestSmells { get; init; } = string.Empty;
    public required AiProvider Provider { get; init; }
    public required GenerationBudgetMode BudgetMode { get; init; }
    public int AttemptNumber { get; init; }
    public bool CompilationSuccess { get; init; }
    public bool TestPassed { get; init; }
    public double CoverageBefore { get; init; }
    public double CoverageAfter { get; init; }
    public double CoverageImprovement { get; init; }
    public double? BaselineMutationScore { get; init; }
    public double? MutationScoreAfter { get; init; }
    public double? MutationScoreImprovement { get; init; }
    public int TotalTokens { get; init; }
    public double DurationSeconds { get; init; }
    public string ErrorLogs { get; init; } = string.Empty;
}

public class ExperimentProjectRow
{
    public required string ProjectName { get; init; }
    public required string ProjectPath { get; init; }
    public int CandidateMethodCount { get; init; }
    public int AttemptCount { get; init; }
    public int SuccessfulTests { get; init; }
}
