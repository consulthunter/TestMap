namespace TestMap.Models.Experiment;

/// <summary>
/// Execution failure category for a generated test attempt.
/// </summary>
public enum TestFailureKind
{
    None,
    Generation,
    Compilation,
    Runtime,
    Assertion,
    Infrastructure,
    Unknown
}
