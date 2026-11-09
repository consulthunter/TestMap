using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class TargetExecutionReportWriterTests
{
    [Fact]
    public async Task InitializeAndUpdate_WritesOneOrderedTerminalRowPerTarget()
    {
        var path = Path.GetTempFileName();
        var first = TargetTestData.Target();
        var second = TargetTestData.Target(TargetTestData.OtherCommit);
        var writer = new TargetExecutionReportWriter(new AtomicFilePublisher());
        try
        {
            await writer.InitializeAsync(path, TargetTestData.Manifest(first, second), TargetTestData.Hash);
            await writer.UpdateAsync(first.TargetId, TargetExecutionStatus.Completed, first.Commit, "complete");
            await writer.UpdateAsync(second.TargetId, TargetExecutionStatus.CommitUnavailable, null, "missing", "materialization", "CommitUnavailable");
            var lines = await File.ReadAllLinesAsync(path);
            Assert.Equal(3, lines.Length);
            Assert.Contains(",Completed,", lines[1], StringComparison.Ordinal);
            Assert.Contains(",CommitUnavailable,", lines[2], StringComparison.Ordinal);
            Assert.DoesNotContain(lines.Skip(1), line => line.Contains(",Pending,", StringComparison.Ordinal));
        }
        finally { File.Delete(path); }
    }
}
