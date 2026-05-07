namespace TestMap.Persistence.Ef.Entities.Coverage;

public class MemberCoverageEntity
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public int CoverageReportId { get; set; }
    public double LineRate { get; set; }
    public double BranchRate { get; set; }
    public int LinesCovered { get; set; }
    public int LinesValid { get; set; }
    public int BranchesCovered { get; set; }
    public int BranchesValid { get; set; }
    public double Complexity { get; set; }
}