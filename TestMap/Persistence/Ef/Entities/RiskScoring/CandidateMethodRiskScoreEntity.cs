using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Persistence.Ef.Entities.RiskScoring;

public class CandidateMethodRiskScoreEntity
{
    public int Id { get; set; }
    public int? CandidateMethodId { get; set; }
    public int MemberId { get; set; }
    public double RiskScore { get; set; }
    public Dictionary<RiskFactorKind, double> FactorScores { get; set; } = new();
    public Dictionary<RiskFactorKind, double> Weights { get; set; } = new();
    public string SelectionReason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}