namespace TestMap.Models.Experiment;

/// <summary>
/// Test generation strategies for the experiment.
/// </summary>
public enum GenerationStrategy
{
    /// <summary>
    /// Generate 1 test, evaluate
    /// </summary>
    Pass1,

    /// <summary>
    /// Generate 5 independent tests, pick best (passes + highest coverage)
    /// </summary>
    Pass5,

    /// <summary>
    /// Generate test, if fails repair with structured errors (up to 5 attempts)
    /// </summary>
    Repair5
}