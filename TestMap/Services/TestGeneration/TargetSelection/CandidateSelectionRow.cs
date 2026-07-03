namespace TestMap.Services.TestGeneration.TargetSelection;

public sealed record CandidateSelectionRow(
    int Id,
    string Name,
    string FullString,
    double LineRate,
    double Complexity);