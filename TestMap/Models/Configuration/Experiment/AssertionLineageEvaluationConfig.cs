namespace TestMap.Models.Configuration.Experiment;

/// <summary>
/// Shared assertion-lineage settings for every enabled experiment lane.
/// The effective values are snapshotted by the immutable measurement policy.
/// </summary>
public sealed class AssertionLineageEvaluationConfig
{
    public const int DefaultMaxDepth = 4;

    public bool Enabled { get; init; } = true;
    public int MaxDepth { get; init; } = DefaultMaxDepth;
}
