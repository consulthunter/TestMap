using TestMap.Models.Experiment.Assertions;

namespace TestMap.Services.StaticAnalysis.Assertions;

public sealed record AssertionSliceResult(
    AssertionLineageCategory Category,
    string ReasonCode,
    int DepthReached,
    IReadOnlyList<int> ProductionMemberIds,
    IReadOnlyList<AssertionLineageStep> Steps)
{
    public static AssertionSliceResult Trivial(
        string reasonCode,
        int depth,
        IReadOnlyList<AssertionLineageStep>? steps = null) =>
        new(
            AssertionLineageCategory.Trivial,
            reasonCode,
            depth,
            [],
            steps ?? []);

    public static AssertionSliceResult Unresolved(
        string reasonCode,
        int depth,
        IReadOnlyList<AssertionLineageStep>? steps = null) =>
        new(
            AssertionLineageCategory.Unresolved,
            reasonCode,
            depth,
            [],
            steps ?? []);
}

public sealed class AssertionLineageClassifier
{
    public AssertionSliceResult CombineComponents(
        IReadOnlyList<AssertionSliceResult> components)
    {
        if (components.Count == 0)
            return AssertionSliceResult.Trivial(
                AssertionLineageReasonCodes.AllInputsTestLocal,
                0);

        var winner = components.Any(component =>
                component.Category == AssertionLineageCategory.Traced)
            ? AssertionLineageCategory.Traced
            : components.Any(component =>
                component.Category == AssertionLineageCategory.Unresolved)
                ? AssertionLineageCategory.Unresolved
                : AssertionLineageCategory.Trivial;

        return Combine(winner, components, winner switch
        {
            AssertionLineageCategory.Traced =>
                components.First(component => component.Category == winner).ReasonCode,
            AssertionLineageCategory.Unresolved =>
                components.First(component => component.Category == winner).ReasonCode,
            _ => AssertionLineageReasonCodes.AllInputsTestLocal
        });
    }

    public AssertionSliceResult CombineAlternatives(
        IReadOnlyList<AssertionSliceResult> alternatives)
    {
        if (alternatives.Count == 0)
            return AssertionSliceResult.Unresolved(
                AssertionLineageReasonCodes.MissingDefinition,
                0);

        var first = alternatives[0].Category;
        if (alternatives.All(alternative =>
                alternative.Category == AssertionLineageCategory.Unresolved) &&
            alternatives.Select(alternative => alternative.ReasonCode).Distinct().Count() == 1)
            return Combine(
                AssertionLineageCategory.Unresolved,
                alternatives,
                alternatives[0].ReasonCode);

        if (alternatives.Any(alternative =>
                alternative.Category == AssertionLineageCategory.Unresolved) ||
            alternatives.Any(alternative => alternative.Category != first))
            return Combine(
                AssertionLineageCategory.Unresolved,
                alternatives,
                AssertionLineageReasonCodes.AmbiguousDefinitions);

        return Combine(
            first,
            alternatives,
            first == AssertionLineageCategory.Trivial
                ? AssertionLineageReasonCodes.AllInputsTestLocal
                : alternatives[0].ReasonCode);
    }

    private static AssertionSliceResult Combine(
        AssertionLineageCategory category,
        IReadOnlyList<AssertionSliceResult> results,
        string reasonCode)
    {
        return new AssertionSliceResult(
            category,
            reasonCode,
            results.Max(result => result.DepthReached),
            results
                .SelectMany(result => result.ProductionMemberIds)
                .Distinct()
                .Order()
                .ToList(),
            results.SelectMany(result => result.Steps).ToList());
    }
}
