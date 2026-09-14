using TestMap.Models.Coverage;

namespace TestMap.Persistence.Ef.Entities.Coverage;

public class CoverageReportEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? TestRunId { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string CollectionStatus { get; set; } = CoverageReportModel.LegacyCollectionStatus;
    public string CollectionReason { get; set; } = string.Empty;
    public string SuccessfulCollector { get; set; } = string.Empty;
    public string CollectionMetadataJson { get; set; } = string.Empty;
    public bool HasUsableCoverage { get; set; }
    public bool LineCountsAvailable { get; set; }
    public bool BranchCountsAvailable { get; set; }
    public string MeasurementPolicyVersion { get; set; } = string.Empty;
    public int RawObjectCount { get; set; }
    public int MappedObjectCount { get; set; }
    public int RawMemberCount { get; set; }
    public int MappedMemberCount { get; set; }
    public double LineRate { get; set; }
    public double BranchRate { get; set; }
    public double Complexity { get; set; }
    public string Version { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public int LinesCovered { get; set; }
    public int LinesValid { get; set; }
    public int BranchesCovered { get; set; }
    public int BranchesValid { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string ScopeKind { get; set; } = "Solution";
    public string ReportRole { get; set; } = TestMap.Models.Testing.TestReportRole.RepositoryBaseline;
    public int? ExperimentRunId { get; set; }
    public string SourceProjectPath { get; set; } = string.Empty;
    public string TestProjectPath { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;

    public virtual TestMap.Persistence.Ef.Entities.Testing.TestRunEntity? TestRun { get; set; }
}
