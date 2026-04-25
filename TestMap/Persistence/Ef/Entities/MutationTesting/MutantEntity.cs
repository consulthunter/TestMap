using TestMap.Models.Code;

namespace TestMap.Persistence.Ef.Entities.MutationTesting;

public class MutantEntity
{
    public int Id { get; set; }
    public int MutationTestingReportId { get; set; }
    public int? MemberId { get; set; }
    public string StrykerMutantId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string MutatorName { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusReason { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public Location Location { get; set; } = new(0, 0, 0, 0);
    public List<string> CoveredBy { get; set; } = new();
    public List<string> KilledBy { get; set; } = new();
    public string ContentHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}