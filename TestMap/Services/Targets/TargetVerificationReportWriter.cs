using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using TestMap.Models.Targets;

namespace TestMap.Services.Targets;

public sealed class TargetVerificationReportWriter(IAtomicFilePublisher publisher)
{
    public async Task WriteAsync(string path, IReadOnlyList<TargetVerificationRecord> records, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\n" })
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { NewLine = "\n" }))
        {
            foreach (var header in new[] { "report_schema_version", "manifest_sha256", "target_id", "repository", "requested_commit", "resolved_commit", "status", "verified_at_utc", "summary" })
                csv.WriteField(header);
            csv.NextRecord();
            foreach (var record in records)
            {
                csv.WriteField(record.ReportSchemaVersion);
                csv.WriteField(record.ManifestSha256);
                csv.WriteField(record.TargetId);
                csv.WriteField(record.Repository);
                csv.WriteField(record.RequestedCommit);
                csv.WriteField(record.ResolvedCommit);
                csv.WriteField(record.Status.ToString());
                csv.WriteField(record.VerifiedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                csv.WriteField(record.Summary);
                csv.NextRecord();
            }
        }
        await publisher.PublishAsync(path, stream.ToArray(), cancellationToken);
    }
}
