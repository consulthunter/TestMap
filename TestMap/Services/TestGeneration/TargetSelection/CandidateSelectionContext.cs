using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Services.TestGeneration.TargetSelection;

public sealed class CandidateSelectionContext
{
    public required ExperimentConfiguration ExperimentConfiguration { get; init; }
    public required TargetSelectionConfig TargetSelection { get; init; }
    public required DateTime SelectionTime { get; init; }
    public required int EffectiveLimit { get; init; }
}