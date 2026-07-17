using System.Text;
using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class TargetRejectionReportWriterTests
{
    [Fact]
    public void Serialize_UsesCanonicalHeaderAndEscapesFields()
    {
        var bytes = new TargetRejectionReportWriter().Serialize([
            new TargetRejectionRecord("1.0", "source.csv", 2, "owner,repo", "\"bad\"", RejectionKind.InvalidCommit, "line one\nline two")
        ]);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("report_schema_version,source_file,source_row,raw_name,raw_commit,rejection_kind,summary\n", text, StringComparison.Ordinal);
        Assert.Contains("\"owner,repo\"", text, StringComparison.Ordinal);
        Assert.False(text.StartsWith("\uFEFF", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateAsync_ManifestPublicationFails_ReportWasPublishedFirst()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-manifest-last-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "targets.csv");
        var output = Path.Combine(directory, "targets.yaml");
        await File.WriteAllTextAsync(input,
            "name,lastCommitSHA\nowner/repository,0123456789abcdef0123456789abcdef01234567\n");
        var publisher = new FailingSecondPublisher();
        var fingerprint = new TargetFingerprintService();
        var service = new TargetManifestCreationService(
            new DelimitedTargetImportService(new TargetIdentityService()),
            new TargetManifestSerializer(fingerprint),
            new TargetRejectionReportWriter(), fingerprint, publisher);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(input, output));
            Assert.Equal(2, publisher.Paths.Count);
            Assert.Contains("-rejections-", publisher.Paths[0], StringComparison.Ordinal);
            Assert.Equal(Path.GetFullPath(output), publisher.Paths[1]);
            Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class FailingSecondPublisher : IAtomicFilePublisher
    {
        public List<string> Paths { get; } = [];

        public async Task PublishAsync(string destinationPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            Paths.Add(Path.GetFullPath(destinationPath));
            if (Paths.Count == 2) throw new IOException("simulated manifest publication failure");
            await new AtomicFilePublisher().PublishAsync(destinationPath, content, cancellationToken);
        }
    }
}
