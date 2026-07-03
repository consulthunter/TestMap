namespace TestMap.Models.Configuration.Experiment;

public abstract class ExperimentEvaluationLaneConfig
{
    public bool Enabled { get; init; } = true;
}

public sealed class TestMapEvaluationConfig : ExperimentEvaluationLaneConfig { }

public sealed class ToolEvaluationConfig : ExperimentEvaluationLaneConfig
{
    public IReadOnlyList<string> ToolIds { get; init; } = [];

    /// <summary>
    /// When true, missing or unavailable tools fail the whole experiment during setup.
    /// When false, unavailable tools are skipped and reported.
    /// </summary>
    public bool RequireAvailabilityInSetup { get; init; } = true;
}

public sealed class ExperimentEvaluationConfig
{
    public TestMapEvaluationConfig TestMap { get; init; } = new();
    public ToolEvaluationConfig Tools { get; init; } = new() { Enabled = false };
}
