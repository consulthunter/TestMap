using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;

namespace TestMap.Services.TestGeneration.TargetSelection;

public sealed class CandidateSelectionContext
{
    public required ExperimentConfig ExperimentConfiguration { get; init; }
    public required TargetSelectionConfig TargetSelection { get; init; }
    public required DateTime SelectionTime { get; init; }
    public required int EffectiveLimit { get; init; }
}
