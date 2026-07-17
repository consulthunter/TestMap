using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using TestMap.Models.Targets;

namespace TestMap.Services.Targets;

public sealed class TargetExecutionReportWriter(IAtomicFilePublisher publisher)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, TargetExecutionRecord> _records = new(StringComparer.Ordinal);
    private IReadOnlyList<string> _order = [];
    private string? _path;

    public async Task InitializeAsync(
        string path,
        TargetManifest manifest,
        string manifestSha256,
        CancellationToken cancellationToken = default)
    {
        _path = path;
        _order = manifest.Targets.Select(target => target.TargetId).ToArray();
        var started = DateTimeOffset.UtcNow;
        foreach (var target in manifest.Targets)
            _records[target.TargetId] = new TargetExecutionRecord(
                "1.0", manifestSha256, target.TargetId, target.Repository, target.Commit, null,
                TargetExecutionStatus.Pending, null, null, "Target is pending execution.", started, null);
        await PublishAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        string targetId,
        TargetExecutionStatus status,
        string? resolvedCommit,
        string summary,
        string? failureStage = null,
        string? failureKind = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = _records.TryGetValue(targetId, out var record)
                ? record
                : throw new InvalidOperationException($"Unknown target execution record '{targetId}'.");
            _records[targetId] = current with
            {
                ResolvedCommit = resolvedCommit,
                Status = status,
                FailureStage = failureStage,
                FailureKind = failureKind,
                Summary = summary,
                CompletedAtUtc = status is TargetExecutionStatus.Pending or TargetExecutionStatus.Materialized ? null : DateTimeOffset.UtcNow
            };
            await PublishUnsafeAsync(cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await PublishUnsafeAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    private async Task PublishUnsafeAsync(CancellationToken cancellationToken)
    {
        if (_path is null) throw new InvalidOperationException("Execution report is not initialized.");
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\n" })
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { NewLine = "\n" }))
        {
            foreach (var header in new[] { "report_schema_version", "manifest_sha256", "target_id", "repository", "requested_commit", "resolved_commit", "status", "failure_stage", "failure_kind", "summary", "started_at_utc", "completed_at_utc" })
                csv.WriteField(header);
            csv.NextRecord();
            foreach (var key in _order)
            {
                var row = _records[key];
                csv.WriteField(row.ReportSchemaVersion); csv.WriteField(row.ManifestSha256); csv.WriteField(row.TargetId);
                csv.WriteField(row.Repository); csv.WriteField(row.RequestedCommit); csv.WriteField(row.ResolvedCommit);
                csv.WriteField(row.Status.ToString()); csv.WriteField(row.FailureStage); csv.WriteField(row.FailureKind);
                csv.WriteField(row.Summary); csv.WriteField(row.StartedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                csv.WriteField(row.CompletedAtUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                csv.NextRecord();
            }
        }
        await publisher.PublishAsync(_path, stream.ToArray(), cancellationToken);
    }
}
