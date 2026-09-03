using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Models.Experiment;
using TestMap.Models.Targets;
using TestMap.Services.StaticAnalysis.Assertions;

namespace TestMap.Services.Experiment.Reporting;

public sealed class ExperimentResultsWriter : IExperimentResultsWriter
{
    public const string DefaultResultsDirectory = "Output";
    public const string DefaultResultsFileName = "experiment-results.csv";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> AppendLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, HashSet<string>> ExistingRowKeys =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] Headers =
    [
        "results_schema_version",
        "row_kind",
        "attempt_id",
        "experiment_run_id",
        "experiment_run_uid",
        "experiment_series_id",
        "candidate_cohort_id",
        "candidate_cohort_member_id",
        "producer_lane",
        "tool_id",
        "tool_run_status",
        "tool_validation_outcome",
        "tool_artifact_path",
        "tool_changed_files_count",
        "tool_production_files_changed",
        "tool_test_files_changed",
        "tool_project_files_changed",
        "tool_deleted_files_count",
        "tool_attempt_id",
        "tool_attempt_targeted_baseline_id",
        "tool_post_attempt_test_run_id",
        "repo_url",
        "repo_owner",
        "repo_name",
        "commit_hash",
        "target_id",
        "repository_identity",
        "requested_commit",
        "resolved_commit",
        "target_manifest_sha256",
        "target_source_sha256",
        "provenance_policy_version",
        "workspace_integrity_status",
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
        "source_member_visibility",
        "access_strategy",
        "access_path_member_ids",
        "test_mapping_count",
        "setup_binding_count",
        "attempt_number",
        "repair_attempt_number",
        "source_member_id",
        "source_method_name",
        "source_method_signature",
        "candidate_test_intentions_summary",
        "candidate_type_construction_summary",
        "candidate_metadata_json",
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
        "outcome_classification",
        "validated_success",
        "validated_evidence_positive",
        "validated_low_impact",
        "impact_evaluable",
        "metric_improved",
        "positive_impact",
        "produced_change",
        "coverage_measurement_status",
        "mutation_measurement_status",
        "impact_measurement_status",
        "impact_attribution",
        "measurement_policy_version",
        "assertion_measurement_status",
        "assertion_measurement_reason",
        "assertion_policy_version",
        "assertion_catalog_version",
        "assertion_max_depth",
        "recognized_assertion_count",
        "unrecognized_assertion_count",
        "traced_assertion_count",
        "trivial_assertion_count",
        "unresolved_assertion_count",
        "no_recognized_assertions",
        "assertion_analysis_duration_ms",
        "assertion_attribution",
        "tool_observed_outcome",
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
        "usage_available",
        "usage_status",
        "usage_source",
        "usage_policy_version",
        "input_tokens",
        "output_tokens",
        "estimated_prompt_tokens",
        "total_tokens",
        "cumulative_input_tokens",
        "cumulative_output_tokens",
        "cumulative_tokens",
        "generation_duration_seconds",
        "validation_duration_seconds",
        "total_attempt_duration_seconds",
        "baseline_test_execution_time_ms",
        "generated_test_execution_time_ms",
        "prompt_version",
        "generation_attempt_id",
        "test_execution_id",
        "generated_test_member_id",
        "test_result_id",
        "resume_stable_key"
    ];

    public async Task WriteAsync(
        ExperimentRun experimentRun,
        IReadOnlyList<ExperimentResultFileRow> rows,
        CancellationToken cancellationToken = default)
    {
        var attemptRows = rows.Where(x => x.RowKind == "attempt").ToList();
        var duplicateAttemptIds = attemptRows
            .GroupBy(x => x.AttemptId, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();
        if (duplicateAttemptIds.Count > 0)
            throw new InvalidOperationException(
                $"Attempt output contains duplicate attempt_id values: {string.Join(", ", duplicateAttemptIds.Take(5))}.");
        ValidateRows(rows);
        var generatedRows = rows
            .Where(x => x.RowKind == "generated_test" ||
                        x.RowKind == "attempt" &&
                        (x.GeneratedTestMemberId.HasValue || !string.IsNullOrWhiteSpace(x.GeneratedTestMethodName)))
            .GroupBy(x => (x.AttemptId, x.GeneratedTestMemberId, x.GeneratedTestMethodName))
            .Select(x => x.First())
            .ToList();
        var testResultRows = rows.Where(x => x.RowKind == "test_result").ToList();

        await WriteRowsAsync(ResolvePath(experimentRun), attemptRows, "attempt", cancellationToken);
        await WriteRowsAsync(ResolveGeneratedTestsPath(experimentRun), generatedRows, "generated_test", cancellationToken);
        await WriteRowsAsync(ResolveTestResultsPath(experimentRun), testResultRows, "test_result", cancellationToken);
        await WriteManifestAsync(experimentRun, rows, cancellationToken);
    }

    public async Task AppendAsync(
        ExperimentRun experimentRun,
        ExperimentResultFileRow row,
        CancellationToken cancellationToken = default)
    {
        ValidateRows([row]);
        if (row.RowKind == "attempt")
        {
            await AppendRowAsync(ResolvePath(experimentRun), row, "attempt", cancellationToken);
            if (row.GeneratedTestMemberId.HasValue || !string.IsNullOrWhiteSpace(row.GeneratedTestMethodName))
                await AppendRowAsync(ResolveGeneratedTestsPath(experimentRun), row, "generated_test", cancellationToken);
        }
        else if (row.RowKind == "generated_test")
        {
            await AppendRowAsync(ResolveGeneratedTestsPath(experimentRun), row, "generated_test", cancellationToken);
        }
        else if (row.RowKind == "test_result")
        {
            await AppendRowAsync(ResolveTestResultsPath(experimentRun), row, "test_result", cancellationToken);
        }

        await WriteManifestAsync(experimentRun, [row], cancellationToken);
    }

    public static string ResolvePath(ExperimentRun experimentRun)
    {
        return string.IsNullOrWhiteSpace(experimentRun.ResultsFilePath)
            ? Path.Combine(DefaultResultsDirectory, $"experiment-{experimentRun.Id}-results.csv")
            : experimentRun.ResultsFilePath;
    }

    public static string ResolveGeneratedTestsPath(ExperimentRun experimentRun) =>
        AddSuffix(ResolvePath(experimentRun), "generated-tests");

    public static string ResolveTestResultsPath(ExperimentRun experimentRun) =>
        AddSuffix(ResolvePath(experimentRun), "test-results");

    public static string ResolveAssertionsPath(ExperimentRun experimentRun) =>
        Path.ChangeExtension(ResolvePath(experimentRun), ".assertions.csv");

    public static string ResolveManifestPath(ExperimentRun experimentRun) =>
        Path.ChangeExtension(ResolvePath(experimentRun), ".manifest.json");

    internal static void ResetAppendCache(string path)
    {
        ExistingRowKeys.TryRemove(Path.GetFullPath(path), out _);
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

    private static string FormatRow(ExperimentResultFileRow row, string? rowKindOverride = null)
    {
        return string.Join(
            ",",
            Escape(row.ResultsSchemaVersion),
            Escape(rowKindOverride ?? row.RowKind),
            Escape(row.AttemptId),
            Escape(row.ExperimentRunId.ToString()),
            Escape(row.ExperimentRunUid),
            Escape(row.ExperimentSeriesId),
            Escape(row.CandidateCohortId?.ToString() ?? string.Empty),
            Escape(row.CandidateCohortMemberId?.ToString() ?? string.Empty),
            Escape(row.ProducerLane),
            Escape(row.ToolId),
            Escape(row.ToolRunStatus),
            Escape(row.ToolValidationOutcome),
            Escape(row.ToolArtifactPath),
            Escape(row.ToolChangedFilesCount.ToString()),
            Escape(row.ToolProductionFilesChanged.ToString()),
            Escape(row.ToolTestFilesChanged.ToString()),
            Escape(row.ToolProjectFilesChanged.ToString()),
            Escape(row.ToolDeletedFilesCount.ToString()),
            Escape(row.ToolAttemptId?.ToString() ?? string.Empty),
            Escape(row.ToolAttemptTargetedBaselineId?.ToString() ?? string.Empty),
            Escape(row.ToolPostAttemptTestRunId?.ToString() ?? string.Empty),
            Escape(row.RepoUrl),
            Escape(row.RepoOwner),
            Escape(row.RepoName),
            Escape(row.CommitHash),
            Escape(row.TargetId),
            Escape(row.RepositoryIdentity),
            Escape(row.RequestedCommit),
            Escape(row.ResolvedCommit),
            Escape(row.TargetManifestSha256),
            Escape(row.TargetSourceSha256),
            Escape(row.ProvenancePolicyVersion),
            Escape(row.WorkspaceIntegrityStatus),
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
            Escape(row.SourceMemberVisibility),
            Escape(row.AccessStrategy),
            Escape(row.AccessPathMemberIds),
            Escape(row.TestMappingCount.ToString()),
            Escape(row.SetupBindingCount.ToString()),
            Escape(row.AttemptNumber.ToString()),
            Escape(row.RepairAttemptNumber?.ToString() ?? string.Empty),
            Escape(row.SourceMemberId.ToString()),
            Escape(row.SourceMethodName),
            Escape(row.SourceMethodSignature),
            Escape(row.CandidateTestIntentionsSummary),
            Escape(row.CandidateTypeConstructionSummary),
            Escape(row.CandidateMetadataJson),
            Escape(row.SourceMethodBaselineCoverage.ToString("R")),
            Escape(row.SourceMethodComplexity.ToString("R")),
            Escape(row.BaselineTestState),
            Escape(row.BaselineTestMethod),
            Escape(row.GeneratedTestMethodName),
            Escape(row.GeneratedTestCompiled.ToString()),
            Escape(row.GeneratedTestExecuted.ToString()),
            Escape(row.GeneratedTestPassed.ToString()),
            Escape(FormatNullable(row.CoverageBefore)),
            Escape(FormatNullable(row.CoverageAfter)),
            Escape(FormatNullable(row.CoverageDelta)),
            Escape(row.MutationScoreBefore?.ToString("R") ?? string.Empty),
            Escape(row.MutationScoreAfter?.ToString("R") ?? string.Empty),
            Escape(row.MutationScoreDelta?.ToString("R") ?? string.Empty),
            Escape(row.MutantKilled?.ToString() ?? string.Empty),
            Escape(row.OutcomeClassification),
            Escape(row.ValidatedSuccess.ToString()),
            Escape(row.ValidatedEvidencePositive.ToString()),
            Escape(row.ValidatedLowImpact.ToString()),
            Escape(row.ImpactEvaluable.ToString()),
            Escape(row.MetricImproved.ToString()),
            Escape(row.PositiveImpact?.ToString() ?? string.Empty),
            Escape(row.ProducedChange.ToString()),
            Escape(row.CoverageMeasurementStatus),
            Escape(row.MutationMeasurementStatus),
            Escape(row.ImpactMeasurementStatus),
            Escape(row.ImpactAttribution),
            Escape(row.MeasurementPolicyVersion),
            Escape(row.AssertionMeasurementStatus),
            Escape(row.AssertionMeasurementReason),
            Escape(row.AssertionPolicyVersion),
            Escape(row.AssertionCatalogVersion),
            Escape(FormatNullable(row.AssertionMaxDepth)),
            Escape(FormatNullable(row.RecognizedAssertionCount)),
            Escape(FormatNullable(row.UnrecognizedAssertionCount)),
            Escape(FormatNullable(row.TracedAssertionCount)),
            Escape(FormatNullable(row.TrivialAssertionCount)),
            Escape(FormatNullable(row.UnresolvedAssertionCount)),
            Escape(row.NoRecognizedAssertions?.ToString() ?? string.Empty),
            Escape(FormatNullable(row.AssertionAnalysisDurationMs)),
            Escape(row.AssertionAttribution),
            Escape(row.ToolObservedOutcome),
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
            Escape(row.UsageAvailable.ToString()),
            Escape(row.UsageStatus),
            Escape(row.UsageSource),
            Escape(row.UsagePolicyVersion),
            Escape(FormatNullable(row.InputTokens)),
            Escape(FormatNullable(row.OutputTokens)),
            Escape(FormatNullable(row.EstimatedPromptTokens)),
            Escape(FormatNullable(row.TotalTokens)),
            Escape(FormatNullable(row.CumulativeInputTokens)),
            Escape(FormatNullable(row.CumulativeOutputTokens)),
            Escape(FormatNullable(row.CumulativeTokens)),
            Escape(row.GenerationDurationSeconds.ToString("R")),
            Escape(row.ValidationDurationSeconds.ToString("R")),
            Escape(row.TotalAttemptDurationSeconds.ToString("R")),
            Escape(FormatNullable(row.BaselineTestExecutionTimeMs)),
            Escape(FormatNullable(row.GeneratedTestExecutionTimeMs)),
            Escape(row.PromptVersion),
            Escape(row.GenerationAttemptId.ToString()),
            Escape(row.TestExecutionId?.ToString() ?? string.Empty),
            Escape(row.GeneratedTestMemberId?.ToString() ?? string.Empty),
            Escape(row.TestResultId?.ToString() ?? string.Empty),
            Escape(row.ResumeStableKey));
    }

    private static async Task WriteRowsAsync(
        string path,
        IReadOnlyCollection<ExperimentResultFileRow> rows,
        string rowKind,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", Headers));
        foreach (var row in rows) builder.AppendLine(FormatRow(row, rowKind));
        await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken);
        ExistingRowKeys.TryRemove(Path.GetFullPath(path), out _);
    }

    private static async Task AppendRowAsync(
        string path,
        ExperimentResultFileRow row,
        string rowKind,
        CancellationToken cancellationToken)
    {
        var normalizedPath = Path.GetFullPath(path);
        var appendLock = AppendLocks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));
        await appendLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(normalizedPath) ?? ".");
            var rowKey = BuildRowKey(
                rowKind,
                row.AttemptId,
                row.GeneratedTestMemberId?.ToString() ?? string.Empty,
                row.GeneratedTestMethodName,
                row.TestResultId?.ToString() ?? string.Empty);
            var existingKeys = ExistingRowKeys.GetOrAdd(normalizedPath, LoadExistingRowKeys);
            if (!existingKeys.Add(rowKey)) return;

            try
            {
                var needsHeader = !File.Exists(normalizedPath) || new FileInfo(normalizedPath).Length == 0;
                await using var stream = new FileStream(normalizedPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                await using var writer = new StreamWriter(stream, Encoding.UTF8);
                if (needsHeader)
                    await writer.WriteLineAsync(string.Join(",", Headers).AsMemory(), cancellationToken);
                await writer.WriteLineAsync(FormatRow(row, rowKind).AsMemory(), cancellationToken);
            }
            catch
            {
                existingKeys.Remove(rowKey);
                throw;
            }
        }
        finally
        {
            appendLock.Release();
        }
    }

    private static HashSet<string> LoadExistingRowKeys(string path)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path) || new FileInfo(path).Length == 0) return keys;

        var records = ParseCsvRecords(File.ReadAllText(path));
        if (records.Count == 0) return keys;
        var headers = records[0]
            .Select((name, index) => (Name: name.TrimStart('\uFEFF'), Index: index))
            .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

        foreach (var fields in records.Skip(1))
        {
            string Get(string name) => headers.TryGetValue(name, out var index) && index < fields.Count
                ? fields[index]
                : string.Empty;

            var rowKind = Get("row_kind");
            if (rowKind is not ("attempt" or "generated_test" or "test_result")) continue;
            keys.Add(BuildRowKey(
                rowKind,
                Get("attempt_id"),
                Get("generated_test_member_id"),
                Get("generated_test_method_name"),
                Get("test_result_id")));
        }

        return keys;
    }

    private static string BuildRowKey(
        string rowKind,
        string attemptId,
        string generatedTestMemberId,
        string generatedTestMethodName,
        string testResultId)
    {
        return rowKind switch
        {
            "attempt" => attemptId,
            "generated_test" => $"{attemptId}|{generatedTestMemberId}|{generatedTestMethodName}",
            "test_result" => $"{attemptId}|{testResultId}|{generatedTestMemberId}|{generatedTestMethodName}",
            _ => throw new InvalidOperationException($"Unknown result row_kind '{rowKind}'.")
        };
    }

    private static List<List<string>> ParseCsvRecords(string text)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
                if (fields.Any(x => x.Length > 0)) records.Add(fields);
                fields = [];
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
            }
            else
            {
                current.Append(character);
            }
        }

        if (current.Length > 0 || fields.Count > 0)
        {
            fields.Add(current.ToString());
            records.Add(fields);
        }

        return records;
    }

    private static async Task WriteManifestAsync(
        ExperimentRun experimentRun,
        IReadOnlyCollection<ExperimentResultFileRow> rows,
        CancellationToken cancellationToken)
    {
        var assertionRow = rows.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.AssertionPolicyVersion) &&
            !string.IsNullOrWhiteSpace(x.AssertionCatalogVersion) &&
            x.AssertionMaxDepth is > 0);
        var assertionPolicyVersion =
            assertionRow?.AssertionPolicyVersion ?? AssertionLineagePolicy.CurrentPolicyVersion;
        var assertionCatalogVersion =
            assertionRow?.AssertionCatalogVersion ?? AssertionLineagePolicy.CurrentCatalogVersion;
        var assertionMaxDepth = assertionRow?.AssertionMaxDepth ?? 4;
        var manifest = new
        {
            results_schema_version = "4.0",
            assertion_schema_version = "1.0",
            experiment_run_uid = experimentRun.RunUid,
            experiment_series_id = experimentRun.ExperimentSeriesId,
            attempt_file = ResolvePath(experimentRun),
            generated_test_file = ResolveGeneratedTestsPath(experimentRun),
            test_result_file = ResolveTestResultsPath(experimentRun),
            assertion_observation_file = ResolveAssertionsPath(experimentRun),
            coverage_unit = "fraction",
            mutation_score_unit = "percentage_points",
            coverage_noise_floor = EvaluationImpactPolicy.CoverageNoiseFloor,
            mutation_noise_floor = EvaluationImpactPolicy.MutationNoiseFloor,
            measurement_policy_version = EvaluationImpactPolicy.Version
            ,assertion_policy_version = assertionPolicyVersion
            ,assertion_catalog_version = assertionCatalogVersion
            ,assertion_effective_max_depth = assertionMaxDepth
            ,assertion_effective_path_cap = AssertionLineagePolicy.DefaultPathCap
            ,assertion_category_definitions = new
            {
                Traced = "At least one observed operand has definite data lineage to a uniquely resolved production member.",
                Trivial = "All observed operands completely terminate in literals or test-local computation.",
                Unresolved = "No operand is definitely traced and at least one required path is incomplete or ambiguous."
            }
            ,assertion_status_definitions = new
            {
                Complete = "All eligible generated tests were analyzed.",
                Partial = "Some eligible generated tests have unavailable evidence.",
                Unavailable = "No trustworthy assertion classification is available.",
                NotApplicable = "The attempt produced no eligible generated or modified test.",
                NotMeasured = "Historical evidence predates this measurement."
            }
            ,assertion_scope = "data-dependence-only"
            ,assertion_control_dependence_credited = false
            ,assertion_non_claim =
                "Traced lineage does not establish logical strength, mutation sensitivity, or assertion-caused mutant kills."
            ,dynamic_measurement_scope =
                "Coverage and mutation retain their independently declared attempt-level attribution."
            ,strict_assertion_publication_audit_passed = (bool?)null
            ,target_id = experimentRun.TargetId
            ,repository_identity = experimentRun.RepositoryIdentity
            ,requested_commit = experimentRun.RequestedCommit
            ,resolved_commit = experimentRun.ResolvedCommit
            ,target_manifest_sha256 = experimentRun.TargetManifestSha256
            ,target_source_sha256 = experimentRun.TargetSourceSha256
            ,provenance_policy_version = experimentRun.ProvenancePolicyVersion
            ,workspace_integrity_status = experimentRun.WorkspaceIntegrityStatus
        };
        var path = ResolveManifestPath(experimentRun);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private static string AddSuffix(string path, string suffix)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        return Path.Combine(directory, $"{fileName}-{suffix}{extension}");
    }

    private static void ValidateRows(IEnumerable<ExperimentResultFileRow> rows)
    {
        foreach (var row in rows)
        {
            if (row.RowKind is not ("attempt" or "generated_test" or "test_result"))
                throw new InvalidOperationException($"Unknown result row_kind '{row.RowKind}'.");
            if (string.IsNullOrWhiteSpace(row.AttemptId))
                throw new InvalidOperationException("Every result row requires a canonical attempt_id.");
            if (string.IsNullOrWhiteSpace(row.ExperimentRunUid))
                throw new InvalidOperationException("Every result row requires experiment_run_uid.");
            if (row.ResultsSchemaVersion != "4.0")
                throw new InvalidOperationException("Pinned evaluation output requires results schema version 4.0.");
            if (string.IsNullOrWhiteSpace(row.TargetId) ||
                string.IsNullOrWhiteSpace(row.RepositoryIdentity) ||
                string.IsNullOrWhiteSpace(row.TargetManifestSha256) ||
                string.IsNullOrWhiteSpace(row.TargetSourceSha256) ||
                string.IsNullOrWhiteSpace(row.ProvenancePolicyVersion))
                throw new InvalidOperationException("Schema 4.0 rows require complete target provenance.");
            if (row.RequestedCommit.Length != 40 || row.ResolvedCommit.Length != 40 ||
                !string.Equals(row.RequestedCommit, row.ResolvedCommit, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Schema 4.0 rows require equal full requested and resolved commits.");
            if (!string.Equals(row.CommitHash, row.ResolvedCommit, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Schema 4.0 commit_hash must equal resolved_commit.");
            if (!Enum.TryParse<WorkspaceIntegrityStatus>(row.WorkspaceIntegrityStatus, true, out var integrity))
                throw new InvalidOperationException("Schema 4.0 rows require a recognized workspace integrity status.");
            if ((row.ValidatedSuccess || row.PositiveImpact == true) && !integrity.IsVerified())
                throw new InvalidOperationException("Validated success and positive impact require verified workspace integrity.");
            ValidateAssertionFields(row);
        }
    }

    private static void ValidateAssertionFields(ExperimentResultFileRow row)
    {
        if (row.RowKind == "test_result")
            return;

        var validStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            "Complete",
            "Partial",
            "Unavailable",
            "NotApplicable"
        };
        if (!validStatuses.Contains(row.AssertionMeasurementStatus))
            throw new InvalidOperationException(
                $"Schema 4.0 {row.RowKind} rows require a recognized assertion measurement status.");

        if (!string.Equals(
                row.AssertionPolicyVersion,
                AssertionLineagePolicy.CurrentPolicyVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                row.AssertionCatalogVersion,
                AssertionLineagePolicy.CurrentCatalogVersion,
                StringComparison.Ordinal) ||
            row.AssertionMaxDepth is not > 0 or > AssertionLineagePolicy.MaximumSupportedDepth)
            throw new InvalidOperationException(
                "Assertion measurements require supported policy, catalog, and maximum depth.");

        if (row.AssertionMeasurementStatus is "Complete" or "Partial")
        {
            if (!row.RecognizedAssertionCount.HasValue ||
                !row.TracedAssertionCount.HasValue ||
                !row.TrivialAssertionCount.HasValue ||
                !row.UnresolvedAssertionCount.HasValue ||
                row.RecognizedAssertionCount.Value !=
                row.TracedAssertionCount.Value +
                row.TrivialAssertionCount.Value +
                row.UnresolvedAssertionCount.Value)
                throw new InvalidOperationException(
                    "Available assertion category counts must reconcile exactly.");
        }

        if (row.AssertionMeasurementStatus is "Unavailable" or "Partial" &&
            !AssertionLineageAuditService.IsStableReasonCode(row.AssertionMeasurementReason))
            throw new InvalidOperationException(
                "Unavailable or partial assertion measurements require a stable reason.");

        if (row.AssertionMeasurementStatus == "Unavailable" &&
            (row.RecognizedAssertionCount.HasValue ||
             row.TracedAssertionCount.HasValue ||
             row.TrivialAssertionCount.HasValue ||
             row.UnresolvedAssertionCount.HasValue))
            throw new InvalidOperationException(
                "Unavailable assertion category counts must be blank, never zero.");
    }

    private static string FormatNullable(int? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    private static string FormatNullable(double? value)
    {
        return value?.ToString("R") ?? string.Empty;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
