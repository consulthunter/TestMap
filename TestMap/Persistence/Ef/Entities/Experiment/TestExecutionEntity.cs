using System.ComponentModel.DataAnnotations;

namespace TestMap.Persistence.Ef.Entities.Experiment;

public class TestExecutionEntity
{
    public int Id { get; set; }
    public int GenerationAttemptId { get; set; }
    public string GeneratedTestCode { get; set; } = string.Empty;
    [MaxLength(500)] public string GeneratedTestMethodName { get; set; } = string.Empty;
    public bool CompilationSucceeded { get; set; }
    public string CompilationErrors { get; set; } = string.Empty;
    public bool TestPassed { get; set; }
    public string RuntimeErrors { get; set; } = string.Empty;
    public string AssertionErrors { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public double FinalCoverage { get; set; }
    public int FinalCoveredLines { get; set; }
    public int FinalTotalLines { get; set; }
    public double CoverageDelta { get; set; }
    public double? BaselineMutationScore { get; set; }
    public double? MutationScoreAfter { get; set; }
    public double? MutationScoreDelta { get; set; }
    public int NewLinesCovered { get; set; }
    [MaxLength(50)] public string TestClassification { get; set; } = string.Empty;
    public DateTime ExecutionTime { get; set; }
    public string StructuredErrors { get; set; } = string.Empty;

    public virtual GenerationAttemptEntity? GenerationAttempt { get; set; }
}