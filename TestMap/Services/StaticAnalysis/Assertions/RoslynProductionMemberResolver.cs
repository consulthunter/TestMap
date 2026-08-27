using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TestMap.Models.Experiment.Assertions;

namespace TestMap.Services.StaticAnalysis.Assertions;

public enum RoslynMemberResolutionKind
{
    Production,
    TestSupport,
    External,
    Ambiguous,
    Reflection,
    Delegate,
    Unknown
}

public sealed record RoslynMemberResolution(
    RoslynMemberResolutionKind Kind,
    ISymbol? Symbol,
    int? MemberId,
    string ReasonCode,
    IReadOnlyList<int> CandidateMemberIds);

public sealed class RoslynProductionMemberResolver
{
    public RoslynMemberResolution Resolve(
        IOperation operation,
        Compilation compilation,
        RoslynMemberSymbolIndex index)
    {
        var symbol = GetReferencedSymbol(operation);
        if (symbol == null)
            return new RoslynMemberResolution(
                RoslynMemberResolutionKind.Unknown,
                null,
                null,
                string.Empty,
                []);

        if (IsReflection(symbol))
            return new RoslynMemberResolution(
                RoslynMemberResolutionKind.Reflection,
                symbol,
                null,
                AssertionLineageReasonCodes.Reflection,
                []);

        if (symbol is IMethodSymbol { MethodKind: MethodKind.DelegateInvoke })
            return new RoslynMemberResolution(
                RoslynMemberResolutionKind.Delegate,
                symbol,
                null,
                AssertionLineageReasonCodes.UnknownDelegateTarget,
                []);

        var normalized = RoslynAnalysisUtilities.NormalizeSymbol(symbol) ?? symbol;
        var directRow = index.TryResolve(normalized);

        if (normalized is IMethodSymbol method &&
            RoslynMemberSymbolIndex.RequiresDispatchResolution(method))
        {
            var candidates = index.ResolveCandidateTargets(method, compilation)
                .Select(candidate => (Symbol: candidate, Row: index.TryResolve(candidate)))
                .Where(candidate => candidate.Row != null)
                .Select(candidate => (candidate.Symbol, Row: candidate.Row!))
                .ToList();
            var implementations = candidates
                .Where(candidate => directRow == null || candidate.Row.MemberId != directRow.MemberId)
                .GroupBy(candidate => candidate.Row.MemberId)
                .Select(group => group.First())
                .ToList();
            // No source implementation is not ambiguity. Any virtual or abstract member of a
            // referenced assembly reaches here -- object.ToString(), TextReader.ReadLine() -- and
            // when nothing in the analysed source overrides it there is exactly one target: the
            // declaration the call already binds to. Reporting Ambiguous for those made every
            // assertion whose operand came from a framework virtual read as Unresolved.
            if (implementations.Count == 0)
                return directRow == null
                    ? new RoslynMemberResolution(
                        RoslynMemberResolutionKind.External,
                        normalized,
                        null,
                        string.Empty,
                        [])
                    : FromRow(normalized, directRow);

            if (implementations.Count > 1)
                return new RoslynMemberResolution(
                    RoslynMemberResolutionKind.Ambiguous,
                    normalized,
                    null,
                    AssertionLineageReasonCodes.AmbiguousDispatch,
                    candidates.Select(candidate => candidate.Row.MemberId).Distinct().Order().ToList());

            return FromRow(implementations[0].Symbol, implementations[0].Row);
        }

        return directRow == null
            ? new RoslynMemberResolution(
                RoslynMemberResolutionKind.External,
                normalized,
                null,
                string.Empty,
                [])
            : FromRow(normalized, directRow);
    }

    private static RoslynMemberResolution FromRow(
        ISymbol symbol,
        RoslynMemberSymbolRow row)
    {
        if (row.IsProduction)
            return new RoslynMemberResolution(
                RoslynMemberResolutionKind.Production,
                symbol,
                row.MemberId,
                ResolveProductionReason(symbol),
                [row.MemberId]);
        if (row.IsTestSupport)
            return new RoslynMemberResolution(
                RoslynMemberResolutionKind.TestSupport,
                symbol,
                row.MemberId,
                string.Empty,
                [row.MemberId]);

        return new RoslynMemberResolution(
            RoslynMemberResolutionKind.External,
            symbol,
            row.MemberId,
            string.Empty,
            [row.MemberId]);
    }

    private static ISymbol? GetReferencedSymbol(IOperation operation)
    {
        return operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            IPropertyReferenceOperation property => property.Property,
            IFieldReferenceOperation field => field.Field,
            IEventReferenceOperation @event => @event.Event,
            IMethodReferenceOperation method => method.Method,
            _ => null
        };
    }

    private static string ResolveProductionReason(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol { MethodKind: MethodKind.Constructor } =>
                AssertionLineageReasonCodes.ProductionConstruction,
            IPropertySymbol or IFieldSymbol or IEventSymbol =>
                AssertionLineageReasonCodes.ProductionValueAccess,
            _ => AssertionLineageReasonCodes.ProductionInvocation
        };
    }

    private static bool IsReflection(ISymbol symbol)
    {
        var containingType = symbol.ContainingType;
        var ns = containingType?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns.StartsWith("System.Reflection", StringComparison.Ordinal) ||
               containingType?.ToDisplayString() == "System.Type" &&
               symbol.Name is "InvokeMember" or "GetMethod" or "GetProperty";
    }
}
