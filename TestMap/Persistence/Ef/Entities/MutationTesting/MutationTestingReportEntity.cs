using TestMap.Models.Results;

namespace TestMap.Persistence.Ef.Entities.MutationTesting;

public class MutationTestingReportEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? TestRunId { get; set; }
    public int? ExperimentRunId { get; set; }
    public string ScopeKind { get; set; } = "Solution";
    public bool IsBaseline { get; set; }
    public string SourceProjectPath { get; set; } = string.Empty;
    public string TestProjectPath { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string ProjectRoot { get; set; } = string.Empty;
    public double MutationScore { get; set; }
    public Dictionary<string, StrykerFileResult> Files { get; set; } = new();
    public Dictionary<string, StrykerTestFileResult> TestFiles { get; set; } = new();
    public StrykerThresholds Thresholds { get; set; } = new();
    public DateTime? CreatedAt { get; set; }

    public virtual TestMap.Persistence.Ef.Entities.Testing.TestRunEntity? TestRun { get; set; }
    public virtual ICollection<MutantEntity> Mutants { get; set; } = new List<MutantEntity>();
}
