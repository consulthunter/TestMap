using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Models.Configuration;

/// <summary>
/// Configuration for experiment-mode execution and shared generation selection settings.
/// </summary>
public class ExperimentConfig
{
    public TargetSelectionStrategy? CandidateSelectionStrategy { get; set; }
    public TestGenerationApproach GenerationApproach { get; set; } =
        TestGenerationApproach.DefaultCoverageExtension;
    public TestActionExecutorMode Executor { get; set; } =
        TestActionExecutorMode.BasicCoverageExtension;
    public List<string> IncludeProviders { get; set; } = new();
    public string? PreferredProvider { get; set; }
    public int CandidateLimit { get; set; } = 3;
    public List<GenerationStrategy> Strategies { get; set; } = new()
    {
        GenerationStrategy.Pass1,
        GenerationStrategy.Pass5,
        GenerationStrategy.Repair5
    };
    public double MinCoverageThreshold { get; set; } = 0.0;
    public double MaxCoverageThreshold { get; set; } = 0.99;
    public string? OutputPath { get; set; }
    public bool IncludeDetailedErrors { get; set; } = true;
    public int StepErrorRetries { get; set; } = 0;
    public int StepRetryDelayMs { get; set; } = 1000;
}
