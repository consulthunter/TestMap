using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using TestMap.Models.Targets;

namespace TestMap.Services.Targets;

public sealed class TargetRejectionReportWriter
{
    public byte[] Serialize(IEnumerable<TargetRejectionRecord> records)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\n" })
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { NewLine = "\n" }))
        {
            foreach (var header in new[] { "report_schema_version", "source_file", "source_row", "raw_name", "raw_commit", "rejection_kind", "summary" })
                csv.WriteField(header);
            csv.NextRecord();
            foreach (var record in records.OrderBy(item => item.SourceRow))
            {
                csv.WriteField(record.ReportSchemaVersion);
                csv.WriteField(record.SourceFile);
                csv.WriteField(record.SourceRow);
                csv.WriteField(record.RawName);
                csv.WriteField(record.RawCommit);
                csv.WriteField(record.RejectionKind.ToString());
                csv.WriteField(record.Summary);
                csv.NextRecord();
            }
        }
        return stream.ToArray();
    }
}
