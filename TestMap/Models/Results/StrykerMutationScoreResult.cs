namespace TestMap.Models.Results;

public record StrykerMutationScoreResult
{
    public int Killed { get; init; }
    public int Survived { get; init; }
    public int Timeout { get; init; }
    public int NoCoverage { get; init; }
    public int Ignored { get; init; }
    public int CompileErrors { get; init; }

    public int TotalDetected => Killed + Timeout + CompileErrors;
    public int TotalUndetected => Survived + NoCoverage;
    public int TotalMutants =>
        Killed + Survived + Timeout + NoCoverage + Ignored + CompileErrors;

    public double Score =>
        (TotalDetected + TotalUndetected) == 0
            ? 0.0
            : (double)TotalDetected / (TotalDetected + TotalUndetected);
}