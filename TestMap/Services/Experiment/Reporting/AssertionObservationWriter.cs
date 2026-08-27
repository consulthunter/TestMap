using System.Collections.Concurrent;
using System.Text;
using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment.Reporting;

public sealed class AssertionObservationFileRow
{
    public string AssertionSchemaVersion { get; init; } = "1.0";
    public int ObservationId { get; init; }
    public string AttemptId { get; init; } = string.Empty;
    public int ExperimentRunId { get; init; }
    public string ExperimentRunUid { get; init; } = string.Empty;
    public string ExperimentSeriesId { get; init; } = string.Empty;
    public string ProducerLane { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string ToolId { get; init; } = string.Empty;
    public int AttemptNumber { get; init; }
    public string RepositoryIdentity { get; init; } = string.Empty;
    public string ResolvedCommit { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string TargetManifestSha256 { get; init; } = string.Empty;
    public string TargetSourceSha256 { get; init; } = string.Empty;
    public int CandidateMethodId { get; init; }
    public int IntendedSourceMemberId { get; init; }
    public int GeneratedTestAssertionSummaryId { get; init; }
    public int? GeneratedTestExecutionId { get; init; }
    public int? ToolAttemptGeneratedTestId { get; init; }
    public int? TestMemberId { get; init; }
    public string TestMemberName { get; init; } = string.Empty;
    public string TestFilePath { get; init; } = string.Empty;
    public string TestMemberContentHash { get; init; } = string.Empty;
    public int AssertionOrdinal { get; init; }
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public int EndLine { get; init; }
    public int EndColumn { get; init; }
    public string Framework { get; init; } = string.Empty;
    public string AssertionStyle { get; init; } = string.Empty;
    public string AssertionMethod { get; init; } = string.Empty;
    public string RecognitionKind { get; init; } = string.Empty;
    public string ExpressionHash { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string ResolutionCode { get; init; } = string.Empty;
    public string TargetRelation { get; init; } = string.Empty;
    public int DepthReached { get; init; }
    public string ResolvedProductionMemberIds { get; init; } = string.Empty;
    public string PolicyVersion { get; init; } = string.Empty;
    public string AssertionCatalogVersion { get; init; } = string.Empty;
    public int MaxDepth { get; init; }
    public string TraceSummary { get; init; } = string.Empty;
    public string OrderedLineagePathsJson { get; init; } = "[]";
    public DateTime AnalyzedAt { get; init; }
}

public sealed class AssertionObservationWriter : IAssertionObservationWriter
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WriteLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] Headers =
    [
        "assertion_schema_version",
        "observation_id",
        "attempt_id",
        "experiment_run_id",
        "experiment_run_uid",
        "experiment_series_id",
        "producer_lane",
        "model",
        "tool_id",
        "attempt_number",
        "repository_identity",
        "resolved_commit",
        "target_id",
        "target_manifest_sha256",
        "target_source_sha256",
        "candidate_method_id",
        "intended_source_member_id",
        "generated_test_assertion_summary_id",
        "generated_test_execution_id",
        "tool_attempt_generated_test_id",
        "test_member_id",
        "test_member_name",
        "test_file_path",
        "test_member_content_hash",
        "assertion_ordinal",
        "start_line",
        "start_column",
        "end_line",
        "end_column",
        "framework",
        "assertion_style",
        "assertion_method",
        "recognition_kind",
        "expression_hash",
        "category",
        "resolution_code",
        "target_relation",
        "depth_reached",
        "resolved_production_member_ids",
        "policy_version",
        "assertion_catalog_version",
        "max_depth",
        "trace_summary",
        "ordered_lineage_paths_json",
        "analyzed_at"
    ];

    public async Task WriteAsync(
        ExperimentRun experimentRun,
        IReadOnlyList<AssertionObservationFileRow> rows,
        CancellationToken cancellationToken = default)
    {
        ValidateRows(experimentRun, rows);
        var path = Path.GetFullPath(ExperimentResultsWriter.ResolveAssertionsPath(experimentRun));
        var writeLock = WriteLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var temporaryPath = path + $".tmp.{Guid.NewGuid():N}";
            try
            {
                await File.WriteAllTextAsync(temporaryPath, FormatFile(rows), Encoding.UTF8, cancellationToken);
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task AppendAsync(
        ExperimentRun experimentRun,
        IReadOnlyList<AssertionObservationFileRow> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0) return;
        ValidateRows(experimentRun, rows);
        var path = Path.GetFullPath(ExperimentResultsWriter.ResolveAssertionsPath(experimentRun));
        var writeLock = WriteLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            if (needsHeader)
                await writer.WriteLineAsync(string.Join(",", Headers).AsMemory(), cancellationToken);
            foreach (var row in rows)
                await writer.WriteLineAsync(FormatRow(row).AsMemory(), cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private static string FormatFile(IReadOnlyList<AssertionObservationFileRow> rows)
    {
        var output = new StringBuilder();
        output.AppendLine(string.Join(",", Headers));
        foreach (var row in rows)
            output.AppendLine(FormatRow(row));
        return output.ToString();
    }

    private static string FormatRow(AssertionObservationFileRow row)
    {
        return string.Join(
            ",",
            Escape(row.AssertionSchemaVersion),
            Escape(row.ObservationId.ToString()),
            Escape(row.AttemptId),
            Escape(row.ExperimentRunId.ToString()),
            Escape(row.ExperimentRunUid),
            Escape(row.ExperimentSeriesId),
            Escape(row.ProducerLane),
            Escape(row.Model),
            Escape(row.ToolId),
            Escape(row.AttemptNumber.ToString()),
            Escape(row.RepositoryIdentity),
            Escape(row.ResolvedCommit),
            Escape(row.TargetId),
            Escape(row.TargetManifestSha256),
            Escape(row.TargetSourceSha256),
            Escape(row.CandidateMethodId.ToString()),
            Escape(row.IntendedSourceMemberId.ToString()),
            Escape(row.GeneratedTestAssertionSummaryId.ToString()),
            Escape(row.GeneratedTestExecutionId?.ToString() ?? string.Empty),
            Escape(row.ToolAttemptGeneratedTestId?.ToString() ?? string.Empty),
            Escape(row.TestMemberId?.ToString() ?? string.Empty),
            Escape(row.TestMemberName),
            Escape(row.TestFilePath),
            Escape(row.TestMemberContentHash),
            Escape(row.AssertionOrdinal.ToString()),
            Escape(row.StartLine.ToString()),
            Escape(row.StartColumn.ToString()),
            Escape(row.EndLine.ToString()),
            Escape(row.EndColumn.ToString()),
            Escape(row.Framework),
            Escape(row.AssertionStyle),
            Escape(row.AssertionMethod),
            Escape(row.RecognitionKind),
            Escape(row.ExpressionHash),
            Escape(row.Category),
            Escape(row.ResolutionCode),
            Escape(row.TargetRelation),
            Escape(row.DepthReached.ToString()),
            Escape(row.ResolvedProductionMemberIds),
            Escape(row.PolicyVersion),
            Escape(row.AssertionCatalogVersion),
            Escape(row.MaxDepth.ToString()),
            Escape(row.TraceSummary),
            Escape(row.OrderedLineagePathsJson),
            Escape(row.AnalyzedAt.ToUniversalTime().ToString("O")));
    }

    private static void ValidateRows(
        ExperimentRun experimentRun,
        IReadOnlyList<AssertionObservationFileRow> rows)
    {
        var duplicateIds = rows.GroupBy(x => x.ObservationId).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
        if (duplicateIds.Count > 0)
            throw new InvalidOperationException(
                $"Assertion sidecar contains duplicate observation IDs: {string.Join(", ", duplicateIds.Take(5))}.");

        foreach (var row in rows)
        {
            if (row.AssertionSchemaVersion != "1.0")
                throw new InvalidOperationException("Assertion sidecar requires schema version 1.0.");
            if (row.ObservationId <= 0 || row.GeneratedTestAssertionSummaryId <= 0)
                throw new InvalidOperationException("Assertion sidecar rows require persisted observation and summary IDs.");
            if (string.IsNullOrWhiteSpace(row.AttemptId) ||
                string.IsNullOrWhiteSpace(row.ExperimentRunUid) ||
                row.ExperimentRunId != experimentRun.Id)
                throw new InvalidOperationException("Assertion sidecar rows require matching attempt and experiment identity.");
            if (string.IsNullOrWhiteSpace(row.RepositoryIdentity) ||
                row.ResolvedCommit.Length != 40 ||
                string.IsNullOrWhiteSpace(row.TargetId) ||
                string.IsNullOrWhiteSpace(row.TargetManifestSha256) ||
                string.IsNullOrWhiteSpace(row.TargetSourceSha256))
                throw new InvalidOperationException("Assertion sidecar rows require complete repository provenance.");
            if (row.Category is not ("Traced" or "Trivial" or "Unresolved"))
                throw new InvalidOperationException($"Unknown assertion category '{row.Category}'.");
            if (string.IsNullOrWhiteSpace(row.ResolutionCode) ||
                string.IsNullOrWhiteSpace(row.PolicyVersion) ||
                string.IsNullOrWhiteSpace(row.AssertionCatalogVersion) ||
                row.MaxDepth <= 0)
                throw new InvalidOperationException("Assertion sidecar rows require resolution and policy provenance.");
            if (!AssertionLineageAuditService.IsStableReasonCode(row.ResolutionCode))
                throw new InvalidOperationException(
                    $"Assertion sidecar resolution code '{row.ResolutionCode}' is not supported.");
            if (row.Category == "Traced" && string.IsNullOrWhiteSpace(row.ResolvedProductionMemberIds))
                throw new InvalidOperationException(
                    "Traced assertion sidecar rows require at least one resolved production member.");
            if (string.IsNullOrWhiteSpace(row.ExpressionHash) ||
                string.IsNullOrWhiteSpace(row.TraceSummary) ||
                string.IsNullOrWhiteSpace(row.OrderedLineagePathsJson))
                throw new InvalidOperationException("Assertion sidecar rows require reproducible trace evidence.");
        }
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
