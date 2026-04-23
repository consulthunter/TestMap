namespace TestMap.Models.MutationTesting;

public class StrykerTestFileResult
{
    public string language { get; set; } = string.Empty;
    public string source { get; set; } = string.Empty;
    public List<StrykerTest> tests { get; set; } = new();
}
