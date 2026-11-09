using TestMap.Services.Targets.Contracts;

namespace TestMap.UnitTests.CLI;

public sealed class TargetCommandsTests
{
    [Fact]
    public async Task Main_TargetsCreate_RequiresExactlyOneInputMode()
    {
        Assert.NotEqual(0, await TestMap.Program.Main(["targets", "create"]));
        Assert.NotEqual(0, await TestMap.Program.Main([
            "targets", "create", "--input", "input.csv", "--url", "https://github.com/owner/repository"
        ]));
    }

    [Fact]
    public async Task Main_TargetsCreate_RejectsModeSpecificOptionsBeforeIo()
    {
        Assert.NotEqual(0, await TestMap.Program.Main([
            "targets", "create", "--url", "https://github.com/owner/repository", "--delimiter", "comma"
        ]));
        Assert.NotEqual(0, await TestMap.Program.Main([
            "targets", "create", "--input", "missing.csv", "--resolution-output", "resolution.yaml"
        ]));
    }

    [Fact]
    public async Task Main_TargetsCreate_RejectsRepeatedUrlBeforeIo()
    {
        var exitCode = await TestMap.Program.Main([
            "targets", "create",
            "--url", "https://github.com/owner/first",
            "--url", "https://github.com/owner/second"
        ]);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task Main_TargetsCreate_ParsesDocumentedOptions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-target-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "input.csv");
        var output = Path.Combine(directory, "manifest.yaml");
        await File.WriteAllTextAsync(input, "name,lastCommitSHA\nowner/repository,0123456789abcdef0123456789abcdef01234567\n");
        try
        {
            var exitCode = await TestMap.Program.Main(["targets", "create", "--input", input, "--output", output, "--delimiter", "comma"]);
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(output));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Main_TargetsVerify_RejectsNonPositiveConcurrencyBeforeVerification()
    {
        var exitCode = await TestMap.Program.Main([
            "targets", "verify", "--file", "does-not-need-to-exist.yaml", "--max-concurrency", "0"
        ]);
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task CreateTargetManifestAsync_DefaultRejectionPath_PublishesCompleteBundle()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-target-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "input.csv");
        var output = Path.Combine(directory, "manifest.yaml");
        await File.WriteAllTextAsync(input, "name,lastCommitSHA\nowner/repository,0123456789abcdef0123456789abcdef01234567\n");
        try
        {
            var result = await TestMap.Program.CreateTargetManifestAsync(input, output, TargetDelimiter.Comma);
            Assert.True(File.Exists(output));
            Assert.True(File.Exists(result.RejectionReportPath));
            Assert.StartsWith("manifest-rejections-", Path.GetFileName(result.RejectionReportPath), StringComparison.Ordinal);
            Assert.Equal(1, result.TotalRecords);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task CreateTargetManifestAsync_NoValidTargets_LeavesManifestUnpublished()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-target-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "input.csv");
        var output = Path.Combine(directory, "manifest.yaml");
        await File.WriteAllTextAsync(input, "name,lastCommitSHA\nowner/repository,short\n");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                TestMap.Program.CreateTargetManifestAsync(input, output, TargetDelimiter.Comma));
            Assert.False(File.Exists(output));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task CreateTargetManifestAsync_CustomRejectionName_RemainsContentAddressed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "testmap-target-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "input.csv");
        await File.WriteAllTextAsync(input, "name,lastCommitSHA\nowner/repository,0123456789abcdef0123456789abcdef01234567\n");
        try
        {
            var result = await TestMap.Program.CreateTargetManifestAsync(
                input, Path.Combine(directory, "manifest.yaml"), TargetDelimiter.Comma,
                Path.Combine(directory, "custom-rejections.csv"));
            Assert.Matches("custom-rejections-[0-9a-f]{12}\\.csv", Path.GetFileName(result.RejectionReportPath));
        }
        finally { Directory.Delete(directory, true); }
    }
}
