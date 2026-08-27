namespace TestMap.Persistence.Ef.Entities.Coverage;

public class ObjectCoverageEntity
{
    public int Id { get; set; }
    public int? ObjectId { get; set; }
    public int CoverageReportId { get; set; }
    public int SourceOrdinal { get; set; } = -1;
    public string PackageName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
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

    public virtual ICollection<MemberCoverageEntity> MemberCoverages { get; set; } = [];
}
