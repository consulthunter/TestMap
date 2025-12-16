namespace TestMap.Models.Results;

public class StrykerTestFileResult
{
    public string language { get; set; }
    public string source { get; set; }
    public List<StrykerTest> tests { get; set; }
}