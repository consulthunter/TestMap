using TestMap.Models.Configuration.Testing.FlakyDetection;
using TestMap.Models.FlakyTestDetection;

namespace TestMap.Persistence.Ef.Entities.FlakyTestDetection;

public class FlakyTestScoreEntity
{
    public int Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public int? TestMemberId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public double FlakinessScore { get; set; }
    public FlakyTestClassification Classification { get; set; } = FlakyTestClassification.InsufficientData;
    public Dictionary<FlakinessFactorKind, double> FactorScores { get; set; } = new();
    public Dictionary<FlakinessFactorKind, double> Weights { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
