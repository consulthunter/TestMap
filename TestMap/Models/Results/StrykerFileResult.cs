namespace TestMap.Models.Results;

public class StrykerFileResult
{
    public string language { get; set; }
    public string source { get; set; }
    public List<StrykerMutant> mutants { get; set; }
}