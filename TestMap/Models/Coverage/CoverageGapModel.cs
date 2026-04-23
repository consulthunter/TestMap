namespace TestMap.Models.Coverage;

public class CoverageGapModel
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public int CoverageReportId { get; set; }
    public int LineNumber { get; set; }
    public int Hits { get; set; }
    public bool IsBranch { get; set; }
    public string ConditionCoverage { get; set; } = string.Empty;
    public CoverageGapKind GapKind { get; set; } = CoverageGapKind.UncoveredLine;
    public string SourceText { get; set; } = string.Empty;
    public string MemberContentHash { get; set; } = string.Empty;
}
