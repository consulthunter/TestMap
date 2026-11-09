namespace TestMap.Persistence.Ef.Entities.Coverage;

public class MemberCoverageEntity
{
    public int Id { get; set; }
    public int? MemberId { get; set; }
    public int CoverageReportId { get; set; }
    public int? ObjectCoverageId { get; set; }
    public int SourceOrdinal { get; set; } = -1;
    public string Name { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string AttributionStatus { get; set; } = string.Empty;
    public string AttributionReason { get; set; } = string.Empty;
    public bool LineCountsAvailable { get; set; }
    public bool BranchCountsAvailable { get; set; }
    public double LineRate { get; set; }
    public double BranchRate { get; set; }
    public int LinesCovered { get; set; }
    public int LinesValid { get; set; }
    public int BranchesCovered { get; set; }
    public int BranchesValid { get; set; }
    public double Complexity { get; set; }

    public virtual ObjectCoverageEntity? ObjectCoverage { get; set; }
}
