using System.Text;
using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class TargetVerificationReportWriterTests
{
    [Fact]
    public async Task WriteAsync_WritesOneOrderedEscapedRowPerTarget()
    {
        var path = Path.GetTempFileName();
        try
        {
            var records = new[]
            {
                Record("b", TargetVerificationStatus.CommitUnavailable, null, "missing, commit"),
                Record("a", TargetVerificationStatus.Available, TargetTestData.Commit, "available")
            };
            await new TargetVerificationReportWriter(new AtomicFilePublisher()).WriteAsync(path, records);
            var text = await File.ReadAllTextAsync(path, Encoding.UTF8);
            Assert.StartsWith("report_schema_version,manifest_sha256,target_id,repository,requested_commit,resolved_commit,status,verified_at_utc,summary\n", text, StringComparison.Ordinal);
            Assert.True(text.IndexOf(",b,", StringComparison.Ordinal) < text.IndexOf(",a,", StringComparison.Ordinal));
            Assert.Contains("\"missing, commit\"", text, StringComparison.Ordinal);
        }
        finally { File.Delete(path); }
    }

    private static TargetVerificationRecord Record(string id, TargetVerificationStatus status, string? resolved, string summary) =>
        new("1.0", TargetTestData.Hash, id, "owner/repository", TargetTestData.Commit, resolved, status,
            new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), summary);
}
