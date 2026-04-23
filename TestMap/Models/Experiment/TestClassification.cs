namespace TestMap.Models.Experiment;

/// <summary>
/// Classification of generated tests based on execution and coverage results.
/// </summary>
public enum TestClassification
{
    /// <summary>
    /// Test passes AND improves coverage (coverage delta > 0%)
    /// </summary>
    Approved,
    
    /// <summary>
    /// Test fails BUT improves coverage (exercises new code)
    /// </summary>
    Candidate,
    
    /// <summary>
    /// Test passes BUT does not improve coverage
    /// </summary>
    Benign,
    
    /// <summary>
    /// Test fails AND does not improve coverage
    /// </summary>
    Failed
}
