using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment.Reporting;

public sealed class ExperimentResultFileRow
{
    public int ExperimentRunId { get; init; }
    public string RepoUrl { get; init; } = string.Empty;
    public string RepoOwner { get; init; } = string.Empty;
    public string RepoName { get; init; } = string.Empty;
    public string CommitHash { get; init; } = string.Empty;
    public DateTime RunDate { get; init; }
    public string Objective { get; init; } = string.Empty;
    public string TargetSelectionStrategy { get; init; } = string.Empty;
    public TestGenerationApproach GenerationApproach { get; init; }
    public MetricsDrivenPath? MetricsPath { get; init; }
    public int? SourceMethodMaintainabilityIndex { get; init; }
    public int? SourceMethodCyclomaticComplexity { get; init; }
    public int? SourceMethodClassCoupling { get; init; }
    public int? SourceMethodDepthOfInheritance { get; init; }
    public int? SourceMethodSourceLinesOfCode { get; init; }
    public int? SourceMethodExecutableLinesOfCode { get; init; }
    public int? BaselineTestMaintainabilityIndex { get; init; }
    public int? BaselineTestCyclomaticComplexity { get; init; }
    public int? BaselineTestClassCoupling { get; init; }
    public int? BaselineTestDepthOfInheritance { get; init; }
    public int? BaselineTestSourceLinesOfCode { get; init; }
    public int? BaselineTestExecutableLinesOfCode { get; init; }
    public int? GeneratedTestMaintainabilityIndex { get; init; }
    public int? GeneratedTestCyclomaticComplexity { get; init; }
    public int? GeneratedTestClassCoupling { get; init; }
    public int? GeneratedTestDepthOfInheritance { get; init; }
    public int? GeneratedTestSourceLinesOfCode { get; init; }
    public int? GeneratedTestExecutableLinesOfCode { get; init; }
    public string BaselineTestSmells { get; init; } = string.Empty;
    public string GeneratedTestSmells { get; init; } = string.Empty;
    public AiProvider Provider { get; init; }
    public string Model { get; init; } = string.Empty;
    public GenerationContextMode ContextMode { get; init; }
    public GenerationBudgetMode BudgetMode { get; init; }
    public string AblationVariantId { get; init; } = string.Empty;
    public string StepsIncluded { get; init; } = string.Empty;
    public int AttemptNumber { get; init; }
    public int? RepairAttemptNumber { get; init; }
    public int SourceMemberId { get; init; }
    public string SourceMethodName { get; init; } = string.Empty;
    public string SourceMethodSignature { get; init; } = string.Empty;
    public double SourceMethodBaselineCoverage { get; init; }
    public double SourceMethodComplexity { get; init; }
    public string BaselineTestState { get; init; } = string.Empty;
    public string BaselineTestMethod { get; init; } = string.Empty;
    public string GeneratedTestMethodName { get; init; } = string.Empty;
    public bool GeneratedTestCompiled { get; init; }
    public bool GeneratedTestExecuted { get; init; }
    public bool GeneratedTestPassed { get; init; }
    public double CoverageBefore { get; init; }
    public double CoverageAfter { get; init; }
    public double CoverageDelta { get; init; }
    public double? MutationScoreBefore { get; init; }
    public double? MutationScoreAfter { get; init; }
    public double? MutationScoreDelta { get; init; }
    public bool? MutantKilled { get; init; }
    public string ToolObservedOutcome { get; init; } = string.Empty;
    public bool? AcceptedByNormalPolicy { get; init; }
    public string FailureKind { get; init; } = string.Empty;
    public string FailureStage { get; init; } = string.Empty;
    public string FailureCategory { get; init; } = string.Empty;
    public string FailureSummary { get; init; } = string.Empty;
    public bool RoslynValidationSucceeded { get; init; } = true;
    public bool RoslynValidationSkipped { get; init; }
    public int RoslynDiagnosticsBeforeCount { get; init; }
    public int RoslynDiagnosticsAfterCount { get; init; }
    public int NewRoslynDiagnosticsCount { get; init; }
    public string NewRoslynDiagnostics { get; init; } = string.Empty;
    public int TotalTokens { get; init; }
    public double TotalDurationSeconds { get; init; }
    public string PromptVersion { get; init; } = string.Empty;
    public int GenerationAttemptId { get; init; }
    public int? TestExecutionId { get; init; }
    public string ResumeStableKey { get; init; } = string.Empty;
}
