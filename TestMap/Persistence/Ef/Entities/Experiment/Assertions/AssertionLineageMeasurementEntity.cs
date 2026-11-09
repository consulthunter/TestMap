using System.ComponentModel.DataAnnotations;
using TestMap.Persistence.Ef.Entities.AgentTools;

namespace TestMap.Persistence.Ef.Entities.Experiment.Assertions;

public sealed class AssertionLineageMeasurementEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int ExperimentRunId { get; set; }
    public int CandidateMethodId { get; set; }
    public int? GenerationAttemptId { get; set; }
    public int? ToolAttemptId { get; set; }
    [MaxLength(50)] public string ProducerLane { get; set; } = string.Empty;
    [MaxLength(50)] public string Status { get; set; } = string.Empty;
    [MaxLength(100)] public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    [MaxLength(100)] public string PolicyVersion { get; set; } = string.Empty;
    [MaxLength(100)] public string AssertionCatalogVersion { get; set; } = string.Empty;
    public int MaxDepth { get; set; }
    public int? EligibleTestCount { get; set; }
    public int? AnalyzedTestCount { get; set; }
    public int? UnavailableTestCount { get; set; }
    public int? NoRecognizedAssertionTestCount { get; set; }
    public int? RecognizedAssertionCount { get; set; }
    public int? UnrecognizedAssertionCount { get; set; }
    public int? TracedAssertionCount { get; set; }
    public int? TrivialAssertionCount { get; set; }
    public int? UnresolvedAssertionCount { get; set; }
    public double? AnalysisDurationMs { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }

    public ExperimentRunEntity? ExperimentRun { get; set; }
    public CandidateMethodEntity? CandidateMethod { get; set; }
    public GenerationAttemptEntity? GenerationAttempt { get; set; }
    public ToolAttemptEntity? ToolAttempt { get; set; }
    public ICollection<GeneratedTestAssertionSummaryEntity> GeneratedTestSummaries { get; set; } =
        new List<GeneratedTestAssertionSummaryEntity>();
}
