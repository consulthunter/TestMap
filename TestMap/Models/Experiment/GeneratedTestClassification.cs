namespace TestMap.Models.Experiment;

/// <summary>
/// Shared classification for generated tests in normal generation and experiments.
/// </summary>
public enum GeneratedTestClassification
{
    ValidatedEvidencePositive,
    ValidatedLowImpact,
    FailedEvidencePositive,
    ValidationFailed
}
