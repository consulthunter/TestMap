using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Services.TestGeneration.TargetSelection.Strategies;

public interface ICandidateSelectionStrategy
{
    TargetSelectionStrategy Strategy { get; }

    Task<IReadOnlyList<CandidateMethod>> SelectAsync(
        CandidateSelectionContext context,
        IReadOnlyList<CandidateSelectionRow> candidatePool,
        CancellationToken cancellationToken = default);
}