namespace TestMap.Models.Configuration.Testing.Generation;

public class TestAcceptanceConfig
{
    public bool RequireCompilationSuccess { get; set; } = true;
    public bool RequireTestsToRun { get; set; } = true;
    public bool RequireAllTestsPass { get; set; } = true;
    public bool RequireCoverageImprovement { get; set; } = true;
    public double MinCoverageImprovement { get; set; } = 0.0;
}
