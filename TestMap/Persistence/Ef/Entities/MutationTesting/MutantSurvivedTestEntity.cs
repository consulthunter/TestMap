using TestMap.Models.Code;

namespace TestMap.Persistence.Ef.Entities.MutationTesting;

public class MutantSurvivedTestEntity
{
    public int Id { get; set; }
    public int MutantId { get; set; }
    public int? TestMemberId { get; set; }
    public string StrykerTestId { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string TestFilePath { get; set; } = string.Empty;
    public Location Location { get; set; } = new(0, 0, 0, 0);
    public string ContentHash { get; set; } = string.Empty;
}
