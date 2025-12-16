namespace TestMap.Models.Results;

public class LizardFileMetrics
{
    public string FilePath { get; init; } = "";
    public int Ncss { get; init; }
    public int Ccn { get; init; }
    public int Functions { get; init; }
}