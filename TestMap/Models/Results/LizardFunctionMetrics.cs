namespace TestMap.Models.Results;

public class LizardFunctionMetrics
{
    public string FunctionName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public int LineNumber { get; init; }

    public int Ncss { get; init; }
    public int Ccn { get; init; }
}