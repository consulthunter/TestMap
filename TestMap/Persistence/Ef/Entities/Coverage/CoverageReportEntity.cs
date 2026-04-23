namespace TestMap.Persistence.Ef.Entities.Coverage;

public class CoverageReportEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
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
}