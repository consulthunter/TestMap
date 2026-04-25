namespace TestMap.Services.TestGeneration.TargetSelection;

public sealed record TestSuiteImprovementCandidateScore(
    int MemberId,
    double Score,
    string RecommendedAction,
    string Evidence);