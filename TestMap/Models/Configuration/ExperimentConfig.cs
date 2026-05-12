using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Models.Configuration;

/// <summary>
/// Configuration for experiment-mode execution and shared generation selection settings.
/// </summary>
public class ExperimentConfig
{
    public TestGenerationObjective Objective { get; set; } = TestGenerationObjective.TestSuiteExpansion;
    public TargetSelectionStrategy? CandidateSelectionStrategy { get; set; }
    public TestContextMappingMode? ContextMappingMode { get; set; }
    public TestGenerationApproach GenerationApproach { get; set; } =
        TestGenerationApproach.MetricsDriven;
    public TestActionExecutorMode Executor { get; set; } =
        TestActionExecutorMode.BasicExtension;
    public List<TestGenerationApproach> Approaches { get; set; } =
    [
        TestGenerationApproach.MetricsDriven
    ];
    public List<MetricsDrivenPath> MetricsPaths { get; set; } =
    [
        MetricsDrivenPath.CoverageAndMutation
    ];
    public List<GenerationBudgetMode> BudgetModes { get; set; } =
    [
        GenerationBudgetMode.PassAt1
    ];
    public bool CompareHistoryModes { get; set; }
    public List<GenerationContextMode> ContextModes { get; set; } =
    [
        GenerationContextMode.ChainedHistory
    ];
    public StepAblationConfig StepAblation { get; set; } = new();
    public double Temperature { get; set; } = 0.0;
    public List<string> IncludeProviders { get; set; } = new();
    public string? PreferredProvider { get; set; }
    public int CandidateLimit { get; set; } = 3;
    public double MinCoverageThreshold { get; set; } = 0.0;
    public double MaxCoverageThreshold { get; set; } = 0.99;
    public string? OutputPath { get; set; }
    public bool IncludeDetailedErrors { get; set; } = true;
    public int StepErrorRetries { get; set; } = 0;
    public int StepRetryDelayMs { get; set; } = 1000;
    public ExperimentResumeConfig Resume { get; set; } = new();
}
