using System.ComponentModel.DataAnnotations;
using TestMap.Persistence.Ef.Entities.AgentTools;

namespace TestMap.Persistence.Ef.Entities.Experiment.Assertions;

public sealed class GeneratedTestAssertionSummaryEntity
{
    public int Id { get; set; }
    public int AssertionLineageMeasurementId { get; set; }
    public int? TestMemberId { get; set; }
    public int? GeneratedTestExecutionId { get; set; }
    public int? ToolAttemptGeneratedTestId { get; set; }
    [MaxLength(500)] public string TestMethodName { get; set; } = string.Empty;
    public string TestFilePath { get; set; } = string.Empty;
    [MaxLength(64)] public string TestMemberContentHash { get; set; } = string.Empty;
    [MaxLength(64)] public string FallbackIdentityHash { get; set; } = string.Empty;
    [MaxLength(50)] public string Status { get; set; } = string.Empty;
    [MaxLength(100)] public string? StatusReason { get; set; }
    public int? RecognizedAssertionCount { get; set; }
    public int? UnrecognizedAssertionCount { get; set; }
    public int? TracedAssertionCount { get; set; }
    public int? TrivialAssertionCount { get; set; }
    public int? UnresolvedAssertionCount { get; set; }

    public AssertionLineageMeasurementEntity? AssertionLineageMeasurement { get; set; }
    public GeneratedTestExecutionEntity? GeneratedTestExecution { get; set; }
    public ToolAttemptGeneratedTestEntity? ToolAttemptGeneratedTest { get; set; }
    public ICollection<AssertionObservationEntity> AssertionObservations { get; set; } =
        new List<AssertionObservationEntity>();
}
