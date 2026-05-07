namespace TestMap.Models.MutationTesting;

public class StrykerFileResult
{
    public string language { get; set; } = string.Empty;
    public string source { get; set; } = string.Empty;
    public List<StrykerMutant> mutants { get; set; } = new();
}