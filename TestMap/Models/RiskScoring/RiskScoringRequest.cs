using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Models.RiskScoring;

public sealed record RiskScoringRequest(
    IReadOnlyList<MemberModel> CandidateMembers,
    TargetSelectionConfig TargetSelectionConfig);
