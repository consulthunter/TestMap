namespace TestMap.Models.Experiment;

/// <summary>
/// Configuration for test generation experiments.
/// </summary>
public class ExperimentConfiguration
{
    /// <summary>
    /// List of AI providers to include in the experiment.
    /// If empty, all available providers will be used.
    /// </summary>
    public List<string> IncludeProviders { get; set; } = new();
    
    /// <summary>
    /// Preferred provider to run first (optional).
    /// </summary>
    public string? PreferredProvider { get; set; }
    
    /// <summary>
    /// Maximum number of candidate methods to select for test generation.
    /// Default: 3
    /// </summary>
    public int CandidateLimit { get; set; } = 3;
    
    /// <summary>
    /// Strategies to execute for each provider.
    /// Default: all strategies (Pass1, Pass5, Repair5)
    /// </summary>
    public List<GenerationStrategy> Strategies { get; set; } = new()
    {
        GenerationStrategy.Pass1,
        GenerationStrategy.Pass5,
        GenerationStrategy.Repair5
    };
    
    /// <summary>
    /// Minimum coverage threshold for a method to be selected (0.0 - 1.0).
    /// Default: 0.0 (any coverage > 0%)
    /// </summary>
    public double MinCoverageThreshold { get; set; } = 0.0;
    
    /// <summary>
    /// Maximum coverage threshold for a method to be selected (0.0 - 1.0).
    /// Default: 0.99 (less than 100%)
    /// </summary>
    public double MaxCoverageThreshold { get; set; } = 0.99;
    
    /// <summary>
    /// Output path for experiment results CSV.
    /// </summary>
    public string? OutputPath { get; set; }
    
    /// <summary>
    /// Whether to include detailed error information in results.
    /// </summary>
    public bool IncludeDetailedErrors { get; set; } = true;

    /// <summary>
    /// Number of retries for an individual generation step when the provider throws an error.
    /// This value is in addition to the initial attempt.
    /// </summary>
    public int StepErrorRetries { get; set; } = 0;

    /// <summary>
    /// Delay in milliseconds between generation step retry attempts.
    /// </summary>
    public int StepRetryDelayMs { get; set; } = 1000;
}
