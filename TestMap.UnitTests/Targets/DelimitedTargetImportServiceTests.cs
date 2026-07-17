using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.UnitTests.Targets;

public sealed class DelimitedTargetImportServiceTests
{
    [Theory]
    [InlineData("comma.csv", TargetDelimiter.Auto, 2)]
    [InlineData("tab.tsv", TargetDelimiter.Auto, 1)]
    [InlineData("bom.csv", TargetDelimiter.Auto, 1)]
    public async Task ImportAsync_SupportedInput_ParsesLogicalRecords(string file, TargetDelimiter delimiter, int total)
    {
        var result = await new DelimitedTargetImportService(new TargetIdentityService()).ImportAsync(Fixture(file), delimiter);
        Assert.Equal(total, result.TotalRecords);
    }

    [Fact]
    public async Task ImportAsync_Duplicates_AggregatesSourceRows()
    {
        var result = await new DelimitedTargetImportService(new TargetIdentityService()).ImportAsync(Fixture("duplicates.csv"));
        var target = Assert.Single(result.Targets);
        Assert.Equal([1, 2], target.SourceRows);
        Assert.Equal(1, result.DeduplicatedRecords);
    }

    [Fact]
    public async Task ImportAsync_AmbiguousDelimiter_RequiresOverride() =>
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new DelimitedTargetImportService(new TargetIdentityService()).ImportAsync(Fixture("ambiguous.txt")));

    [Fact]
    public async Task ImportAsync_MissingHeaders_FailsContract()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "repository,sha\nowner/repo," + TargetTestData.Commit);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new DelimitedTargetImportService(new TargetIdentityService()).ImportAsync(path, TargetDelimiter.Comma));
        }
        finally { File.Delete(path); }
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Targets", name);
}
