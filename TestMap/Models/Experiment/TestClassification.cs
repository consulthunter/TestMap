namespace TestMap.Models.Experiment;

/// <summary>
/// Tool-observed outcome for generated tests based on validation and configured evidence.
/// </summary>
public enum TestClassification
{
    /// <summary>
    /// Validation passed and at least one configured evidence signal improved.
    /// </summary>
    ValidatedEvidencePositive,

    /// <summary>
    /// Validation passed, but configured evidence signal was absent or below threshold.
    /// </summary>
    ValidatedLowImpact,

    /// <summary>
    /// Validation failed, but partial evidence indicates the artifact may still be useful.
    /// </summary>
    FailedEvidencePositive,

    /// <summary>
    /// Validation failed and no useful configured evidence was observed.
    /// </summary>
    ValidationFailed
}
