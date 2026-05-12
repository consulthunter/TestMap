using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Services.TestGeneration.TargetSelection.Strategies;

public sealed class ExistingCandidateSelectionStrategy : ICandidateSelectionStrategy
{
    public TargetSelectionStrategy Strategy => TargetSelectionStrategy.Existing;

    public Task<IReadOnlyList<CandidateMethod>> SelectAsync(
        CandidateSelectionContext context,
        IReadOnlyList<CandidateSelectionRow> candidatePool,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CandidateMethod> candidates = candidatePool
            .Select(row => CandidateMethodFactory.Create(row, context.SelectionTime))
            .ToList();

        return Task.FromResult(candidates);
    }
}
