using System.Text;
using TestMap.Models.Configuration;
using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment.Reporting;

public sealed class ExperimentResultsWriter : IExperimentResultsWriter
{
    public const string DefaultResultsDirectory = "Output";
    public const string DefaultResultsFileName = "experiment-results.csv";

    private static readonly string[] Headers =
    [
        "experiment_run_id",
        "repo_url",
        "repo_owner",
        "repo_name",
        "commit_hash",
        "run_date",
        "objective",
        "target_selection_strategy",
        "generation_approach",
        "source_method_mi",
        "source_method_cc",
        "source_method_coupling",
        "source_method_dit",
        "source_method_sloc",
        "source_method_eloc",
        "baseline_test_mi",
        "baseline_test_cc",
        "baseline_test_coupling",
        "baseline_test_dit",
        "baseline_test_sloc",
        "baseline_test_eloc",
        "generated_test_mi",
        "generated_test_cc",
        "generated_test_coupling",
        "generated_test_dit",
        "generated_test_sloc",
        "generated_test_eloc",
        "baseline_test_smells",
        "generated_test_smells",
        "provider",
        "model",
        "context_mode",
        "budget_mode",
        "ablation_variant_id",
        "steps_included",
        "attempt_number",
        "repair_attempt_number",
        "source_member_id",
        "source_method_name",
        "source_method_signature",
        "source_method_baseline_coverage",
        "source_method_complexity",
        "baseline_test_state",
        "baseline_test_method",
        "generated_test_method_name",
        "generated_test_compiled",
        "generated_test_executed",
        "generated_test_passed",
        "coverage_before",
        "coverage_after",
        "coverage_delta",
        "mutation_score_before",
        "mutation_score_after",
        "mutation_score_delta",
        "mutant_killed",
        "tool_observed_outcome",
        "accepted_by_normal_policy",
        "failure_kind",
        "failure_stage",
        "failure_category",
        "failure_summary",
        "roslyn_validation_succeeded",
        "roslyn_validation_skipped",
        "roslyn_diagnostics_before_raw_count",
        "roslyn_diagnostics_after_raw_count",
        "new_actionable_roslyn_diagnostics_count",
        "new_roslyn_diagnostics",
        "total_tokens",
        "total_duration_seconds",
        "prompt_version",
        "generation_attempt_id",
        "test_execution_id",
        "resume_stable_key"
    ];

    public async Task WriteAsync(
        ExperimentRun experimentRun,
        IReadOnlyList<ExperimentResultFileRow> rows,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(experimentRun);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", Headers));
        foreach (var row in rows)
            builder.AppendLine(FormatRow(row));

        await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken);
    }

    public async Task AppendAsync(
        ExperimentRun experimentRun,
        ExperimentResultFileRow row,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(experimentRun);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;

        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream, Encoding.UTF8);

        if (needsHeader)
            await writer.WriteLineAsync(string.Join(",", Headers).AsMemory(), cancellationToken);

        await writer.WriteLineAsync(FormatRow(row).AsMemory(), cancellationToken);
    }

    public static string ResolvePath(ExperimentRun experimentRun)
    {
        return string.IsNullOrWhiteSpace(experimentRun.ResultsFilePath)
            ? Path.Combine(DefaultResultsDirectory, $"experiment-{experimentRun.Id}-results.csv")
            : experimentRun.ResultsFilePath;
    }

    public static string ResolveResultsFilePath(ExperimentConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.OutputPath))
            return Path.Combine(DefaultResultsDirectory, DefaultResultsFileName);

        var outputPath = config.OutputPath.Trim();
        return Path.GetExtension(outputPath).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? outputPath
            : Path.Combine(outputPath, DefaultResultsFileName);
    }

    private static string FormatRow(ExperimentResultFileRow row)
    {
        return string.Join(
            ",",
            Escape(row.ExperimentRunId.ToString()),
            Escape(row.RepoUrl),
            Escape(row.RepoOwner),
            Escape(row.RepoName),
            Escape(row.CommitHash),
            Escape(row.RunDate.ToString("O")),
            Escape(row.Objective),
            Escape(row.TargetSelectionStrategy),
            Escape(row.GenerationApproach.ToString()),
            Escape(FormatNullable(row.SourceMethodMaintainabilityIndex)),
            Escape(FormatNullable(row.SourceMethodCyclomaticComplexity)),
            Escape(FormatNullable(row.SourceMethodClassCoupling)),
            Escape(FormatNullable(row.SourceMethodDepthOfInheritance)),
            Escape(FormatNullable(row.SourceMethodSourceLinesOfCode)),
            Escape(FormatNullable(row.SourceMethodExecutableLinesOfCode)),
            Escape(FormatNullable(row.BaselineTestMaintainabilityIndex)),
            Escape(FormatNullable(row.BaselineTestCyclomaticComplexity)),
            Escape(FormatNullable(row.BaselineTestClassCoupling)),
            Escape(FormatNullable(row.BaselineTestDepthOfInheritance)),
            Escape(FormatNullable(row.BaselineTestSourceLinesOfCode)),
            Escape(FormatNullable(row.BaselineTestExecutableLinesOfCode)),
            Escape(FormatNullable(row.GeneratedTestMaintainabilityIndex)),
            Escape(FormatNullable(row.GeneratedTestCyclomaticComplexity)),
            Escape(FormatNullable(row.GeneratedTestClassCoupling)),
            Escape(FormatNullable(row.GeneratedTestDepthOfInheritance)),
            Escape(FormatNullable(row.GeneratedTestSourceLinesOfCode)),
            Escape(FormatNullable(row.GeneratedTestExecutableLinesOfCode)),
            Escape(row.BaselineTestSmells),
            Escape(row.GeneratedTestSmells),
            Escape(row.Provider.ToString()),
            Escape(row.Model),
            Escape(row.ContextMode.ToString()),
            Escape(row.BudgetMode.ToString()),
            Escape(row.AblationVariantId),
            Escape(row.StepsIncluded),
            Escape(row.AttemptNumber.ToString()),
            Escape(row.RepairAttemptNumber?.ToString() ?? string.Empty),
            Escape(row.SourceMemberId.ToString()),
            Escape(row.SourceMethodName),
            Escape(row.SourceMethodSignature),
            Escape(row.SourceMethodBaselineCoverage.ToString("R")),
            Escape(row.SourceMethodComplexity.ToString("R")),
            Escape(row.BaselineTestState),
            Escape(row.BaselineTestMethod),
            Escape(row.GeneratedTestMethodName),
            Escape(row.GeneratedTestCompiled.ToString()),
            Escape(row.GeneratedTestExecuted.ToString()),
            Escape(row.GeneratedTestPassed.ToString()),
            Escape(row.CoverageBefore.ToString("R")),
            Escape(row.CoverageAfter.ToString("R")),
            Escape(row.CoverageDelta.ToString("R")),
            Escape(row.MutationScoreBefore?.ToString("R") ?? string.Empty),
            Escape(row.MutationScoreAfter?.ToString("R") ?? string.Empty),
            Escape(row.MutationScoreDelta?.ToString("R") ?? string.Empty),
            Escape(row.MutantKilled?.ToString() ?? string.Empty),
            Escape(row.ToolObservedOutcome),
            Escape(row.AcceptedByNormalPolicy?.ToString() ?? string.Empty),
            Escape(row.FailureKind),
            Escape(row.FailureStage),
            Escape(row.FailureCategory),
            Escape(row.FailureSummary),
            Escape(row.RoslynValidationSucceeded.ToString()),
            Escape(row.RoslynValidationSkipped.ToString()),
            Escape(row.RoslynDiagnosticsBeforeCount.ToString()),
            Escape(row.RoslynDiagnosticsAfterCount.ToString()),
            Escape(row.NewRoslynDiagnosticsCount.ToString()),
            Escape(row.NewRoslynDiagnostics),
            Escape(row.TotalTokens.ToString()),
            Escape(row.TotalDurationSeconds.ToString("R")),
            Escape(row.PromptVersion),
            Escape(row.GenerationAttemptId.ToString()),
            Escape(row.TestExecutionId?.ToString() ?? string.Empty),
            Escape(row.ResumeStableKey));
    }

    private static string FormatNullable(int? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
