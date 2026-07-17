using System.Diagnostics;
using TestMap.Models.Targets;
using TestMap.Services.Targets;
using Xunit.Abstractions;

namespace TestMap.IntegrationTests.Targets;

public sealed class LicenseFilteredTargetImportTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Import_MinimalExampleAndTenThousandTargetFixture_AccountsForEveryRecord()
    {
        var sourcePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Targets",
            "example-project.csv");
        var service = new DelimitedTargetImportService(new TargetIdentityService());

        var sourceMeasurement = await MeasureAsync(() => service.ImportAsync(sourcePath));
        Assert.Equal(1, sourceMeasurement.Result.TotalRecords);
        var example = Assert.Single(sourceMeasurement.Result.Targets);
        Assert.Equal("consulthunter/testmap-example", example.Repository);
        Assert.Equal("867ab17d3141bc0e6d696486bd8edb67546c00d1", example.Commit);
        Assert.Equal(1, sourceMeasurement.Result.Targets.Count + sourceMeasurement.Result.Rejections.Count + sourceMeasurement.Result.DeduplicatedRecords);

        var generatedPath = Path.GetTempFileName();
        try
        {
            await using (var writer = new StreamWriter(generatedPath))
            {
                await writer.WriteLineAsync("name,lastCommitSHA");
                for (var index = 0; index < 10_000; index++)
                    await writer.WriteLineAsync($"owner{index}/repository,{index:x8}0123456789abcdef0123456789abcdef");
            }
            var generatedMeasurement = await MeasureAsync(() => service.ImportAsync(generatedPath));
            Assert.Equal(10_000, generatedMeasurement.Result.TotalRecords);
            Assert.Equal(10_000, generatedMeasurement.Result.Targets.Count);
            output.WriteLine($"source_1 elapsed_ms={sourceMeasurement.Elapsed.TotalMilliseconds:F1} memory_delta_bytes={sourceMeasurement.MemoryDelta}");
            output.WriteLine($"generated_10000 elapsed_ms={generatedMeasurement.Elapsed.TotalMilliseconds:F1} memory_delta_bytes={generatedMeasurement.MemoryDelta}");
        }
        finally { File.Delete(generatedPath); }
    }

    private static async Task<(TargetImportResult Result, TimeSpan Elapsed, long MemoryDelta)> MeasureAsync(
        Func<Task<TargetImportResult>> operation)
    {
        GC.Collect();
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var stopwatch = Stopwatch.StartNew();
        var result = await operation();
        stopwatch.Stop();
        return (result, stopwatch.Elapsed, Math.Max(0, GC.GetTotalMemory(false) - before));
    }

}
