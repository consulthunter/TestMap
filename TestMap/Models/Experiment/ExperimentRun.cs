namespace TestMap.Models.Experiment;

/// <summary>
/// Represents a complete experiment run across multiple providers and strategies.
/// </summary>
public class ExperimentRun
{
    public int Id { get; set; }
    public string RunUid { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProjectId { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string CandidateSelectionStrategy { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty;
    public string ResultsFilePath { get; set; } = string.Empty;
    public int CandidateLimit { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ExperimentSeriesId { get; set; } = string.Empty;
    public int? CandidateCohortId { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public string RepositoryIdentity { get; set; } = string.Empty;
    public string RequestedCommit { get; set; } = string.Empty;
    public string ResolvedCommit { get; set; } = string.Empty;
    public string TargetManifestSha256 { get; set; } = string.Empty;
    public string TargetSourceSha256 { get; set; } = string.Empty;
    public DateTime? MaterializedAtUtc { get; set; }
    public string WorkspaceIntegrityStatus { get; set; } = string.Empty;
    public string ProvenancePolicyVersion { get; set; } = string.Empty;
    public string AssertionLineagePolicyVersion { get; set; } = string.Empty;
    public string AssertionCatalogVersion { get; set; } = string.Empty;
    public int AssertionLineageMaxDepth { get; set; }
    public int AssertionLineagePathCap { get; set; }

    public virtual ICollection<CandidateMethod> CandidateMethods { get; set; } = new List<CandidateMethod>();
}
